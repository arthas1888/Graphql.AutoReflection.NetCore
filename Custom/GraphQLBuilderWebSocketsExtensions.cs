using GraphQL.Server;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using GraphQL.Server.Transports.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using SER.Graphql.Reflection.NetCore.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Custom
{
    public static class GraphQLBuilderWebSocketsExtensions
    {
        /// <summary>
        /// Add required services for GraphQL web sockets
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IGraphQLBuilder AddCustomWebSockets(this IGraphQLBuilder builder)
        {
            builder.Services
                .AddTransient(typeof(IWebSocketConnectionFactory<>), typeof(WebSocketConnectionFactory<>))
                .AddTransient<IOperationMessageListener, LogMessagesListener>()
                .AddTransient<IOperationMessageListener, ProtocolMessageListener>()
                .AddTransient<IOperationMessageListener, AuthenticationListener>();

            return builder;
        }
    }
}
