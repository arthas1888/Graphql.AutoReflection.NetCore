

using GraphQL;
using GraphQL.Execution;
using GraphQLParser.AST;

namespace SER.Graphql.Reflection.NetCore.WebSocket
{
    class MyDocumentExecuter: DocumentExecuter
    {
        protected override IExecutionStrategy SelectExecutionStrategy(ExecutionContext context)
        {
            return context.Operation.Operation switch
            {
                OperationType.Query => ParallelExecutionStrategy.Instance,
                OperationType.Mutation => SerialExecutionStrategy.Instance,
                //OperationType.Subscription => SubscriptionExecutionStrategy.Instance,
                _ => base.SelectExecutionStrategy(context)
            };
        }
    }
}
