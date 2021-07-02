using GraphQL;
using GraphQL.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.WebSocket
{
    class MyDocumentExecuter: DocumentExecuter
    {
        protected override IExecutionStrategy SelectExecutionStrategy(ExecutionContext context)
        {
            return context.Operation.OperationType switch
            {
                GraphQL.Language.AST.OperationType.Query => ParallelExecutionStrategy.Instance,
                GraphQL.Language.AST.OperationType.Mutation => SerialExecutionStrategy.Instance,
                GraphQL.Language.AST.OperationType.Subscription => SubscriptionExecutionStrategy.Instance,
                _ => base.SelectExecutionStrategy(context)
            };
        }
    }
}
