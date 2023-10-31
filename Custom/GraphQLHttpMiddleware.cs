using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Instrumentation;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Net.Http.Headers;
using System.Threading;
using GraphQL;
using GraphQL.Server.Transports.AspNetCore;
using System.Net;
using GraphQL.Validation;
using Microsoft.Extensions.Options;
using GraphQL.Introspection;
using GraphQL.Execution;
using System.Text;
using GraphQL.DataLoader;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using GraphQL.Transport;

namespace SER.Graphql.Reflection.NetCore.Custom
{
    public class GraphQLHttpMiddleware<TSchema>
        where TSchema : ISchema
    {
        private const string DOCS_URL = "See: http://graphql.org/learn/serving-over-http/.";

        private readonly RequestDelegate _next;
        private readonly PathString _path;
        private readonly IGraphQLTextSerializer _serializer;
        private FillDataExtensions _fillDataExtensions;
        private readonly ILogger _logger;

        private const string MEDIATYPE_GRAPHQLJSON = "application/graphql+json"; // deprecated
        private const string MEDIATYPE_JSON = "application/json";
        private const string MEDIATYPE_GRAPHQL = "application/graphql";

        public GraphQLHttpMiddleware(
            RequestDelegate next,
            PathString path,
            IGraphQLTextSerializer serializer,
            ILoggerFactory loggerFactory)
        {
            _next = next;
            _path = path;
            _logger = loggerFactory.CreateLogger<GraphQLHttpMiddleware<TSchema>>();
            _serializer = serializer;
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

            var cancellationToken = GetCancellationToken(context);

            // GraphQL HTTP only supports GET and POST methods
            bool isGet = HttpMethods.IsGet(httpRequest.Method);
            bool isPost = HttpMethods.IsPost(httpRequest.Method);
            if (!isGet && !isPost)
            {
                httpResponse.Headers["Allow"] = "GET, POST";
                await HandleInvalidHttpMethodErrorAsync(context);
                return;
            }

            // Parse POST body
            GraphQLRequest bodyGQLRequest = null;
            IList<GraphQLRequest> bodyGQLBatchRequest = null;
            if (isPost)
            {
                if (!MediaTypeHeaderValue.TryParse(httpRequest.ContentType, out var mediaTypeHeader))
                {
                    await HandleContentTypeCouldNotBeParsedErrorAsync(context);
                    return;
                }

                switch (mediaTypeHeader.MediaType)
                {
                    case MEDIATYPE_JSON:
                        IList<GraphQLRequest> deserializationResult;
                        try
                        {
#if NET5_0_OR_GREATER
                            if (!TryGetEncoding(mediaTypeHeader.CharSet, out var sourceEncoding))
                            {
                                await HandleContentTypeCouldNotBeParsedErrorAsync(context);
                                return;
                            }
                            // Wrap content stream into a transcoding stream that buffers the data transcoded from the sourceEncoding to utf-8.
                            if (sourceEncoding != null && sourceEncoding != System.Text.Encoding.UTF8)
                            {
                                using var tempStream = System.Text.Encoding.CreateTranscodingStream(httpRequest.Body, innerStreamEncoding: sourceEncoding, outerStreamEncoding: System.Text.Encoding.UTF8, leaveOpen: true);
                                deserializationResult = await _serializer.ReadAsync<IList<GraphQLRequest>>(tempStream, cancellationToken);
                            }
                            else
                            {
                                deserializationResult = await _serializer.ReadAsync<IList<GraphQLRequest>>(httpRequest.Body, cancellationToken);
                            }
#else
                        deserializationResult = await _serializer.ReadAsync<IList<GraphQLRequest>>(httpRequest.Body, cancellationToken);
#endif
                        }
                        catch (Exception ex)
                        {
                            if (!await HandleDeserializationErrorAsync(context, ex))
                                throw;
                            return;
                        }
                        // https://github.com/graphql-dotnet/server/issues/751
                        if (deserializationResult is GraphQLRequest[] array && array.Length == 1)
                            bodyGQLRequest = deserializationResult[0];
                        else
                            bodyGQLBatchRequest = deserializationResult;
                        break;

                    case MEDIATYPE_GRAPHQL:
                        bodyGQLRequest = await DeserializeFromGraphBodyAsync(httpRequest.Body);
                        break;

                    default:
                        if (httpRequest.HasFormContentType)
                        {
                            var formCollection = await httpRequest.ReadFormAsync(cancellationToken);
                            try
                            {
                                bodyGQLRequest = DeserializeFromFormBody(formCollection);
                            }
                            catch (Exception ex)
                            {
                                if (!await HandleDeserializationErrorAsync(context, ex))
                                    throw;
                                return;
                            }
                            break;
                        }
                        await HandleInvalidContentTypeErrorAsync(context);
                        return;
                }
            }

            // If we don't have a batch request, parse the query from URL too to determine the actual request to run.
            // Query string params take priority.
            GraphQLRequest gqlRequest = null;
            if (bodyGQLBatchRequest == null)
            {
                GraphQLRequest urlGQLRequest = null;
                try
                {
                    urlGQLRequest = DeserializeFromQueryString(httpRequest.Query);
                }
                catch (Exception ex)
                {
                    if (!await HandleDeserializationErrorAsync(context, ex))
                        throw;
                    return;
                }

                gqlRequest = new GraphQLRequest
                {
                    Query = urlGQLRequest.Query ?? bodyGQLRequest?.Query,
                    Variables = urlGQLRequest.Variables ?? bodyGQLRequest?.Variables,
                    Extensions = urlGQLRequest.Extensions ?? bodyGQLRequest?.Extensions,
                    OperationName = urlGQLRequest.OperationName ?? bodyGQLRequest?.OperationName
                };
            }

            // Prepare context and execute
            var userContextBuilder = context.RequestServices.GetService<IUserContextBuilder>();
            var userContext = userContextBuilder == null
                ? new Dictionary<string, object>() // in order to allow resolvers to exchange their state through this object
                : await userContextBuilder.BuildUserContextAsync(context, null);

            var rules = context.RequestServices.GetServices<IValidationRule>();

            var executer = context.RequestServices.GetRequiredService<IDocumentExecuter<TSchema>>();
            await HandleRequestAsync(context, _next, userContext, bodyGQLBatchRequest, gqlRequest, executer, rules, cancellationToken);

        }

        protected virtual async Task HandleRequestAsync(
        HttpContext context,
        RequestDelegate next,
        IDictionary<string, object> userContext,
        IList<GraphQLRequest> bodyGQLBatchRequest,
        GraphQLRequest gqlRequest,
        IDocumentExecuter<TSchema> executer,
        IEnumerable<IValidationRule> rules,
        CancellationToken cancellationToken)
        {
            // Normal execution with single graphql request
            if (bodyGQLBatchRequest == null)
            {
                var stopwatch = ValueStopwatch.StartNew();
                await RequestExecutingAsync(gqlRequest);
                var result = await ExecuteRequestAsync(gqlRequest, userContext, executer, context.RequestServices, rules, cancellationToken);

                //await RequestExecutedAsync(new GraphQLRequestExecutionResult(gqlRequest, result, stopwatch.Elapsed));

                await WriteResponseAsync(context.Response, _serializer, cancellationToken, result);
            }
            // Execute multiple graphql requests in one batch
            else
            {
                var executionResults = new ExecutionResult[bodyGQLBatchRequest.Count];
                for (int i = 0; i < bodyGQLBatchRequest.Count; ++i)
                {
                    var gqlRequestInBatch = bodyGQLBatchRequest[i];

                    var stopwatch = ValueStopwatch.StartNew();
                    await RequestExecutingAsync(gqlRequestInBatch, i);
                    var result = await ExecuteRequestAsync(gqlRequestInBatch, userContext, executer, context.RequestServices, rules, cancellationToken);

                    //await RequestExecutedAsync(new GraphQLRequestExecutionResult(gqlRequestInBatch, result, stopwatch.Elapsed, i));

                    executionResults[i] = result;
                }

                await WriteResponseAsync(context.Response, _serializer, cancellationToken, executionResults);
            }
        }

        protected virtual async ValueTask<bool> HandleDeserializationErrorAsync(HttpContext context, Exception ex)
        {
            await WriteErrorResponseAsync(context, $"JSON body text could not be parsed. {ex.Message}", HttpStatusCode.BadRequest);
            return true;
        }

        protected virtual Task HandleNoQueryErrorAsync(HttpContext context)
            => WriteErrorResponseAsync(context, "GraphQL query is missing.", HttpStatusCode.BadRequest);

        protected virtual Task HandleContentTypeCouldNotBeParsedErrorAsync(HttpContext context)
            => WriteErrorResponseAsync(context, $"Invalid 'Content-Type' header: value '{context.Request.ContentType}' could not be parsed.", HttpStatusCode.UnsupportedMediaType);

        protected virtual Task HandleInvalidContentTypeErrorAsync(HttpContext context)
            => WriteErrorResponseAsync(context, $"Invalid 'Content-Type' header: non-supported media type '{context.Request.ContentType}'. Must be of '{MEDIATYPE_JSON}', '{MEDIATYPE_GRAPHQL}' or body form. {DOCS_URL}", HttpStatusCode.UnsupportedMediaType);

        protected virtual Task HandleInvalidHttpMethodErrorAsync(HttpContext context)
            => WriteErrorResponseAsync(context, $"Invalid HTTP method. Only GET and POST are supported. {DOCS_URL}", HttpStatusCode.MethodNotAllowed);


        protected virtual Task<ExecutionResult> ExecuteRequestAsync(
            GraphQLRequest gqlRequest,
            IDictionary<string, object> userContext,
            IDocumentExecuter<TSchema> executer,
            IServiceProvider requestServices,
            IEnumerable<IValidationRule> rules,
            CancellationToken token)
        => executer.ExecuteAsync(new ExecutionOptions
        {
            Query = gqlRequest.Query,
            OperationName = gqlRequest.OperationName,
            Variables = gqlRequest.Variables,
            Extensions = gqlRequest.Extensions,
            UserContext = userContext,
            RequestServices = requestServices,
            CancellationToken = token,
            ValidationRules = rules,
        });

        protected virtual CancellationToken GetCancellationToken(HttpContext context) => context.RequestAborted;

        protected virtual Task RequestExecutingAsync(GraphQLRequest request, int? indexInBatch = null)
        {
            // nothing to do in this middleware
            return Task.CompletedTask;
        }
        

        private async Task WriteErrorResponseAsync(HttpContext context, string errorMessage, HttpStatusCode httpStatusCode /* BadRequest */)
        {
            var result = new ExecutionResult
            {
                Errors = new ExecutionErrors
                {
                    new ExecutionError(errorMessage)
                }
            };
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)httpStatusCode;
            if ((int)httpStatusCode == 400)
                _logger.LogError($"_______________________________EEEEEEEEEEEEEEEEEEEEEErrrrrrrrrrrrrrrrrrrrrrrrrr {errorMessage}");

            //if (!string.IsNullOrEmpty(errorMessage) && errorMessage.Equals("The operation was canceled."))
            //    return;

            await _serializer.WriteAsync(context.Response.Body, result, GetCancellationToken(context));
        }


        protected virtual Task WriteResponseAsync<TResult>(HttpResponse httpResponse, IGraphQLSerializer serializer, CancellationToken cancellationToken, TResult result)
        {
            httpResponse.ContentType = "application/json";
            httpResponse.StatusCode = result is not ExecutionResult executionResult || executionResult.Executed ? 200 : 400; // BadRequest when fails validation; OK otherwise

            if (_fillDataExtensions.GetAll().Count > 0 && result is ExecutionResult)
                (result as ExecutionResult).Extensions = _fillDataExtensions.GetAll().ToDictionary(entry => entry.Key, entry => entry.Value);

            if (httpResponse.StatusCode == (int)HttpStatusCode.BadRequest)
                _logger.LogError($"_______________________________ Error Graph request {string.Join(", ", (result as ExecutionResult).Errors.Select(x => x.Message).ToList())} ");

            var forbiddenCode = "auth-required";
            var authorizationCode = "authorization";
            var notFoundCode = "not-found";

            var errors = new ExecutionErrors();
            if (result is ExecutionResult)
            {
                httpResponse.StatusCode = (result as ExecutionResult).Errors?.Any(er => (er as ValidationError)?.Code == authorizationCode || (er as ValidationError)?.Number == authorizationCode) == true
                    ? (int)HttpStatusCode.Unauthorized
                    : (result as ExecutionResult).Errors?.Any(er => (er as ValidationError)?.Code == forbiddenCode || (er as ValidationError)?.Number == forbiddenCode) == true ?
                    (int)HttpStatusCode.Forbidden
                    : (result as ExecutionResult).Errors?.Any(er => (er as ValidationError)?.Code == notFoundCode || (er as ValidationError)?.Number == notFoundCode) == true ?
                    (int)HttpStatusCode.NotFound
                    : httpResponse.StatusCode;

                if ((result as ExecutionResult).Errors != null)
                    foreach (var error in (result as ExecutionResult).Errors)
                    {
                        _logger.LogError($"_______________________________ Error Graph request {error.Message} InnerException {error.InnerException} ----------------");
                        var ex = new ExecutionError(error.Message);
                        if (error.InnerException != null)
                            ex = new ExecutionError(error.InnerException.Message, error.InnerException);
                        errors.Add(ex);
                    }
                if (errors.Count > 0) (result as ExecutionResult).Errors?.AddRange(errors);
            }

            return serializer.WriteAsync(httpResponse.Body, result, cancellationToken);
        }

        private const string QUERY_KEY = "query";
        private const string VARIABLES_KEY = "variables";
        private const string EXTENSIONS_KEY = "extensions";
        private const string OPERATION_NAME_KEY = "operationName";

        private GraphQLRequest DeserializeFromQueryString(IQueryCollection queryCollection) => new GraphQLRequest
        {
            Query = queryCollection.TryGetValue(QUERY_KEY, out var queryValues) ? queryValues[0] : null,
            Variables = queryCollection.TryGetValue(VARIABLES_KEY, out var variablesValues) ? _serializer.Deserialize<Inputs>(variablesValues[0]) : null,
            Extensions = queryCollection.TryGetValue(EXTENSIONS_KEY, out var extensionsValues) ? _serializer.Deserialize<Inputs>(extensionsValues[0]) : null,
            OperationName = queryCollection.TryGetValue(OPERATION_NAME_KEY, out var operationNameValues) ? operationNameValues[0] : null
        };

        private GraphQLRequest DeserializeFromFormBody(IFormCollection formCollection) => new GraphQLRequest
        {
            Query = formCollection.TryGetValue(QUERY_KEY, out var queryValues) ? queryValues[0] : null,
            Variables = formCollection.TryGetValue(VARIABLES_KEY, out var variablesValues) ? _serializer.Deserialize<Inputs>(variablesValues[0]) : null,
            Extensions = formCollection.TryGetValue(EXTENSIONS_KEY, out var extensionsValues) ? _serializer.Deserialize<Inputs>(extensionsValues[0]) : null,
            OperationName = formCollection.TryGetValue(OPERATION_NAME_KEY, out var operationNameValues) ? operationNameValues[0] : null
        };


        private async Task<GraphQLRequest> DeserializeFromGraphBodyAsync(Stream bodyStream)
        {
            // In this case, the query is the raw value in the POST body

            // Do not explicitly or implicitly (via using, etc.) call dispose because StreamReader will dispose inner stream.
            // This leads to the inability to use the stream further by other consumers/middlewares of the request processing
            // pipeline. In fact, it is absolutely not dangerous not to dispose StreamReader as it does not perform any useful
            // work except for the disposing inner stream.
            string query = await new StreamReader(bodyStream).ReadToEndAsync();

            return new GraphQLRequest { Query = query }; // application/graphql MediaType supports only query text
        }

#if NET5_0_OR_GREATER
        private static bool TryGetEncoding(string charset, out System.Text.Encoding encoding)
        {
            encoding = null;

            if (string.IsNullOrEmpty(charset))
                return true;

            try
            {
                // Remove at most a single set of quotes.
                if (charset.Length > 2 && charset[0] == '\"' && charset[^1] == '\"')
                {
                    encoding = System.Text.Encoding.GetEncoding(charset[1..^1]);
                }
                else
                {
                    encoding = System.Text.Encoding.GetEncoding(charset);
                }
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }
#endif
    }
}