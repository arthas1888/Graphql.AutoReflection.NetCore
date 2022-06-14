using GraphQL.Types;
using GraphQLParser;
using GraphQLParser.AST;
using SER.Graphql.Reflection.NetCore.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.CustomScalar
{
    public class MyBooleanGraphType : BooleanGraphType
    {
        public MyBooleanGraphType()
        {
            Name = "Boolean";
        }

        /// <inheritdoc/>
        public override object ParseLiteral(GraphQLValue value) => value switch
        {
            GraphQLBooleanValue b => b.Value == "true",
            GraphQLIntValue i => ParseValue(i.Value),
            GraphQLStringValue s => ParseValue(s.Value),
            GraphQLFloatValue f => ParseValue(f.Value),
            GraphQLNullValue _ => null,
            _ => ThrowLiteralConversionError(value)
        };


        /// <inheritdoc/>
        public override bool CanParseLiteral(GraphQLValue value)
            => value is GraphQLBooleanValue || value is GraphQLNullValue;

        /// <inheritdoc/>
        public override object ParseValue(object value) => value switch
        {
            bool _ => value,
            string _ => bool.Parse(value.ToString()),
            ROM _ => ((ROM)value).ParseLiteral(),
            null => null,
            _ => ThrowValueConversionError(value)
        };


    }

}
