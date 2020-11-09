using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Instrumentation;
using GraphQL.Server.Internal;
using GraphQL.Server.Transports.AspNetCore.Common;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Net.Http.Headers;
using System.Threading;
using GraphQL;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Server.Common;
using System.Net;
using GraphQL.Validation;
using GraphQL.Server;
using Microsoft.Extensions.Options;
using GraphQL.Introspection;
using GraphQL.Execution;
using System.Text;
using GraphQL.DataLoader;

namespace SER.Graphql.Reflection.NetCore.Custom
{
    public class GraphQLHttpMiddleware<TSchema>
        where TSchema : ISchema
    {
        private const string DOCS_URL = "See: http://graphql.org/learn/serving-over-http/.";

        private readonly RequestDelegate _next;
        private readonly PathString _path;
        private readonly IGraphQLRequestDeserializer _deserializer;
        private readonly GraphQLOptions _options;
        private FillDataExtensions _fillDataExtensions;
        private readonly IEnumerable<IDocumentExecutionListener> _listeners;
        private readonly GraphQLSettings _settings;
        private readonly IDocumentWriter _writer;

        public GraphQLHttpMiddleware(
            RequestDelegate next,
            PathString path,
            GraphQLSettings settings,
            IGraphQLRequestDeserializer deserializer,
            IEnumerable<IDocumentExecutionListener> listeners,
            IOptions<GraphQLOptions> options,
            IDocumentWriter writer)
        {
            _next = next;
            _path = path;
            _deserializer = deserializer;
            _options = options.Value;
            _listeners = listeners;
            _settings = settings;
            _writer = writer;
        }

        public async Task InvokeAsync(HttpContext context, ISchema schema, FillDataExtensions fillDataExtensions)
        {
            if (context.WebSockets.IsWebSocketRequest || !context.Request.Path.StartsWithSegments(_path))
            {
                await _next(context);
                return;
            }
            _fillDataExtensions = fillDataExtensions;
            // Handle requests as per recommendation at http://graphql.org/learn/serving-over-http/
            // Inspiration: https://github.com/graphql/express-graphql/blob/master/src/index.js
            var httpRequest = context.Request;
            var httpResponse = context.Response;

            //var writer = context.RequestServices.GetRequiredService<IDocumentWriter>();
            var cancellationToken = GetCancellationToken(context);

            // GraphQL HTTP only supports GET and POST methods
            bool isGet = HttpMethods.IsGet(httpRequest.Method);
            bool isPost = HttpMethods.IsPost(httpRequest.Method);
            if (!isGet && !isPost)
            {
                httpResponse.Headers["Allow"] = "GET, POST";
                await WriteErrorResponseAsync(context,
                    $"Invalid HTTP method. Only GET and POST are supported. {DOCS_URL}",
                    httpStatusCode: 405 // Method Not Allowed
                );
                return;
            }

            // Parse POST body
            GraphQLRequest bodyGQLRequest = null;
            GraphQLRequest[] bodyGQLBatchRequest = null;
            if (isPost)
            {
                if (!MediaTypeHeaderValue.TryParse(httpRequest.ContentType, out var mediaTypeHeader))
                {
                    await WriteErrorResponseAsync(context, $"Invalid 'Content-Type' header: value '{httpRequest.ContentType}' could not be parsed.");
                    return;
                }

                switch (mediaTypeHeader.MediaType)
                {
                    case MediaType.JSON:
                        var deserializationResult = await _deserializer.DeserializeFromJsonBodyAsync(httpRequest, cancellationToken);
                        if (!deserializationResult.IsSuccessful)
                        {
                            await WriteErrorResponseAsync(context, "Body text could not be parsed. Body text should start with '{' for normal graphql query or with '[' for batched query.");
                            return;
                        }

                        bodyGQLRequest = deserializationResult.Single;
                        //bodyGQLBatchRequest = deserializationResult.Batch;
                        break;

                    case MediaType.GRAPH_QL:
                        bodyGQLRequest = await DeserializeFromGraphBodyAsync(httpRequest.Body);
                        break;

                    case MediaType.FORM:
                        var formCollection = await httpRequest.ReadFormAsync();
                        bodyGQLRequest = DeserializeFromFormBody(formCollection);
                        break;

                    default:
                        await WriteErrorResponseAsync(context, $"Invalid 'Content-Type' header: non-supported media type. " +
                            $"Must be of '{MediaType.JSON}', '{MediaType.GRAPH_QL}' or '{MediaType.FORM}'. {DOCS_URL}");
                        return;
                }
            }

            // If we don't have a batch request, parse the URL too to determine the actual request to run
            // Querystring params take priority
            GraphQLRequest gqlRequest = null;
            if (bodyGQLBatchRequest == null)
            {
                // Parse URL
                var urlGQLRequest = DeserializeFromQueryString(httpRequest.Query);

                gqlRequest = new GraphQLRequest
                {
                    Query = urlGQLRequest.Query ?? bodyGQLRequest?.Query,
                    Inputs = urlGQLRequest.Inputs ?? bodyGQLRequest?.Inputs,
                    OperationName = urlGQLRequest.OperationName ?? bodyGQLRequest?.OperationName
                };
            }

            // Prepare context and execute
            var userContextBuilder = context.RequestServices.GetService<IUserContextBuilder>();
            var userContext = userContextBuilder == null
                ? new Dictionary<string, object>() // in order to allow resolvers to exchange their state through this object
                : await userContextBuilder.BuildUserContext(context);

            var executer = context.RequestServices.GetRequiredService<IGraphQLExecuter<TSchema>>();
            // var executer = context.RequestServices.GetRequiredService<IDocumentExecuter>();

            // Normal execution with single graphql request
            if (bodyGQLBatchRequest == null)
            {
                var stopwatch = ValueStopwatch.StartNew();
                // var result = await ExecuteRequestAsync(schema, gqlRequest, context, executer, cancellationToken);
                var result = await ExecuteRequestAsync(gqlRequest, userContext, executer, cancellationToken);


                await RequestExecutedAsync(new GraphQLRequestExecutionResult(gqlRequest, result, stopwatch.Elapsed));

                if (result.Errors?.Count > 0)
                {
                    await WriteErrorResponseAsync(context, result);
                }
                else
                {
                    await WriteResponseAsync(context, result);
                }

            }
            // Execute multiple graphql requests in one batch
            else
            {
                var error = false;
                ExecutionResult executionResult = null;
                var executionResults = new ExecutionResult[bodyGQLBatchRequest.Length];
                for (int i = 0; i < bodyGQLBatchRequest.Length; ++i)
                {
                    var gqlRequestInBatch = bodyGQLBatchRequest[i];

                    var stopwatch = ValueStopwatch.StartNew();
                    // var result = await ExecuteRequestAsync(schema, gqlRequestInBatch, context, executer, cancellationToken);
                    var result = await ExecuteRequestAsync(gqlRequestInBatch, userContext, executer, cancellationToken);

                    await RequestExecutedAsync(new GraphQLRequestExecutionResult(gqlRequestInBatch, result, stopwatch.Elapsed, i));
                    if (result.Errors?.Count > 0)
                    {
                        error = true;
                        executionResult = result;
                    }
                    executionResults[i] = result;
                }

                if (error)
                {
                    await WriteErrorResponseAsync(context, executionResult);
                }
                else
                {
                    // await WriteResponseAsync(context, executionResults);
                }
            }
        }


        private static Task<ExecutionResult> ExecuteRequestAsync(GraphQLRequest gqlRequest, IDictionary<string, object> userContext,
            IGraphQLExecuter<TSchema> executer, CancellationToken token)
            => executer.ExecuteAsync(
                gqlRequest.OperationName,
                gqlRequest.Query,
                gqlRequest.Inputs,
                userContext,
                token);

        private Task<ExecutionResult> ExecuteRequestAsync(
            ISchema schema,
            GraphQLRequest gqlRequest,
            HttpContext context,
            IDocumentExecuter executer,
            CancellationToken token)
            => executer.ExecuteAsync(_ =>
            {
                _.Schema = schema;
                _.Query = gqlRequest.Query;
                _.OperationName = gqlRequest.OperationName;
                _.Inputs = gqlRequest.Inputs;
                _.UserContext = _settings.BuildUserContext?.Invoke(context);
                _.ValidationRules = DocumentValidator.CoreRules.Concat(_settings.ValidationRules);
                _.ComplexityConfiguration = _options.ComplexityConfiguration;
                _.EnableMetrics = _options.EnableMetrics;
                _.ExposeExceptions = _options.ExposeExceptions;
                _.UnhandledExceptionDelegate = _options.UnhandledExceptionDelegate;
                _.SchemaFilter = _options.SchemaFilter ?? new DefaultSchemaFilter();
                _.CancellationToken = token;

                foreach (var listener in _listeners)
                {
                    _.Listeners.Add(listener);
                }
                var listenerData = context.RequestServices.GetRequiredService<DataLoaderDocumentListener>();
                _.Listeners.Add(listenerData);
                if (_settings.EnableMetrics)
                {
                    _.FieldMiddleware.Use<InstrumentFieldsMiddleware>();
                }
            });

        protected virtual CancellationToken GetCancellationToken(HttpContext context) => context.RequestAborted;

        protected virtual Task RequestExecutedAsync(in GraphQLRequestExecutionResult requestExecutionResult)
        {
            // nothing to do in this middleware
            return Task.CompletedTask;
        }

        private async Task WriteErrorResponseAsync(HttpContext context,
            string errorMessage, int httpStatusCode = 400 /* BadRequest */)
        {
            var result = new ExecutionResult
            {
                Errors = new ExecutionErrors
                {
                    new ExecutionError(errorMessage)
                }
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = httpStatusCode;

            await _writer.WriteAsync(context.Response.Body, result);
        }

        private async Task WriteErrorResponseAsync(HttpContext context,
           ExecutionResult result, int httpStatusCode = 400 /* BadRequest */)
        {
            var forbiddenCode = "auth-required";
            var authorizationCode = "authorization";
            var notFoundCode = "not-found";

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = result.Errors?.Any(er => (er as ValidationError)?.Code == authorizationCode) == true
                ? (int)HttpStatusCode.Unauthorized
                : result.Errors?.Any(er => (er as ValidationError)?.Code == forbiddenCode) == true ?
                (int)HttpStatusCode.Forbidden
                : result.Errors?.Any(er => (er as ValidationError)?.Code == notFoundCode) == true ?
                (int)HttpStatusCode.NotFound
                : httpStatusCode;

            var errors = new ExecutionErrors();
            var msg = "";
            foreach (var error in result.Errors)
            {
                msg = error.Message;
                Console.WriteLine($"_______________________________EEEEEEEEEEEEEEEEEEEEEErrrrrrrrrrrrrrrrrrrrrrrrrr {error}");
                var ex = new ExecutionError(error.Message);
                if (error.InnerException != null)
                {
                    ex = new ExecutionError(error.Message, error.InnerException);
                }
                errors.Add(ex);
            }
            if (!string.IsNullOrEmpty(msg) && msg.Equals("The operation was canceled."))
                return;

            result.Data = errors;
            try
            {
                await _writer.WriteAsync(context.Response.Body, result);
            }
            catch (System.Text.Json.JsonException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private async Task WriteResponseAsync(HttpContext context, ExecutionResult result)
        {
            if (_fillDataExtensions.GetAll().Count > 0)
                result.Extensions = _fillDataExtensions.GetAll();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = result.Errors?.Any() == true ? (int)HttpStatusCode.BadRequest : (int)HttpStatusCode.OK;

            await _writer.WriteAsync(context.Response.Body, result);
        }

        //private Task WriteResponseAsync<TResult>(HttpContext context, TResult result)
        //{
        //    if (result is ExecutionResult && _fillDataExtensions.GetAll().Count > 0)
        //        (result as ExecutionResult).Extensions = _fillDataExtensions.GetAll();
        //    /*var mediaType = new MediaTypeHeaderValue("application/json");
        //    mediaType.CharSet = "utf-8";*/
        //    context.Response.ContentType = "application/json"; // mediaType.ToString(); //
        //    context.Response.StatusCode = 200; // OK

        //    return _writer.WriteAsync(context.Response.Body, result);
        //}

        private GraphQLRequest DeserializeFromQueryString(IQueryCollection queryCollection) => new GraphQLRequest
        {
            Query = queryCollection.TryGetValue(GraphQLRequest.QUERY_KEY, out var queryValues) ? queryValues[0] : null,
            Inputs = queryCollection.TryGetValue(GraphQLRequest.VARIABLES_KEY, out var variablesValues) ? _deserializer.DeserializeInputsFromJson(variablesValues[0]) : null,
            OperationName = queryCollection.TryGetValue(GraphQLRequest.OPERATION_NAME_KEY, out var operationNameValues) ? operationNameValues[0] : null
        };

        private GraphQLRequest DeserializeFromFormBody(IFormCollection formCollection) => new GraphQLRequest
        {
            Query = formCollection.TryGetValue(GraphQLRequest.QUERY_KEY, out var queryValues) ? queryValues[0] : null,
            Inputs = formCollection.TryGetValue(GraphQLRequest.VARIABLES_KEY, out var variablesValue) ? _deserializer.DeserializeInputsFromJson(variablesValue[0]) : null,
            OperationName = formCollection.TryGetValue(GraphQLRequest.OPERATION_NAME_KEY, out var operationNameValues) ? operationNameValues[0] : null
        };

        private async Task<GraphQLRequest> DeserializeFromGraphBodyAsync(Stream bodyStream)
        {
            // In this case, the query is the raw value in the POST body

            // Do not explicitly or implicitly (via using, etc.) call dispose because StreamReader will dispose inner stream.
            // This leads to the inability to use the stream further by other consumers/middlewares of the request processing
            // pipeline. In fact, it is absolutely not dangerous not to dispose StreamReader as it does not perform any useful
            // work except for the disposing inner stream.
            string query = await new StreamReader(bodyStream).ReadToEndAsync();

            return new GraphQLRequest { Query = query };
        }
    }
}