using GraphQL.Types;
using GraphQLParser.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.CustomScalar
{
    public class MyIntGraphType : IntGraphType
    {
        public MyIntGraphType()
        {
            Name = "Integer";
        }

        public override object ParseLiteral(GraphQLValue value) => value switch
        {
            GraphQLStringValue s => ParseValue(s.Value),
            GraphQLIntValue intValue => intValue.Value,
            GraphQLNullValue _ => null,
            _ => ThrowLiteralConversionError(value)
        };
        
        /// <inheritdoc/>
        public override bool CanParseLiteral(GraphQLValue value) => value switch
        {
            GraphQLIntValue x => int.TryParse(x.Value, out var _),
            GraphQLStringValue str => int.TryParse(str.Value, out int @res),
            GraphQLNullValue _ => true,
            _ => false
        };

        public override object ParseValue(object value) => value switch
        {
            int _ => value,
            null => null,
            sbyte sb => checked((int)sb),
            byte b => checked((int)b),
            short s => checked((int)s),
            ushort us => checked((int)us),
            uint ui => checked((int)ui),
            long l => checked((int)l),
            ulong ul => checked((int)ul),
            string str => int.Parse(str),
            BigInteger bi => checked((int)bi),
            _ => ThrowValueConversionError(value)
        };

        public override bool CanParseValue(object value)
        {
            try
            {
                _ = ParseValue(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }


    public class MyLongGraphType : LongGraphType
    {
        public MyLongGraphType()
        {
            Name = "Long";
        }

        public override object ParseLiteral(GraphQLValue value) => value switch
        {
            GraphQLStringValue s => ParseValue(s.Value),
            GraphQLIntValue intValue => intValue.Value,
            //GraphQLLongValue longValue => longValue.Value,
            //GraphQLBigIntValue bigIntValue => checked((long)bigIntValue.Value),
            GraphQLNullValue _ => null,
            _ => ThrowLiteralConversionError(value)
        };

        public override bool CanParseLiteral(GraphQLValue value)
        {
            try
            {
                _ = ParseLiteral(value);
                return (value) switch
                {
                    GraphQLIntValue _ => true,
                    GraphQLStringValue str => long.TryParse(str.Value, out long @res),
                    //GraphQLLongValue longValue => long.MinValue <= longValue.Value && longValue.Value <= long.MaxValue,
                    //GraphQLBigIntValue bigIntValue => long.MinValue <= bigIntValue.Value && bigIntValue.Value <= long.MaxValue,
                    GraphQLNullValue _ => true,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        public override object ParseValue(object value) => value switch
        {
            long _ => value,

            null => null,
            sbyte sb => checked((long)sb),
            byte b => checked((long)b),
            short s => checked((long)s),
            ushort us => checked((long)us),
            uint ui => checked((long)ui),
            int i => checked((long)i),
            ulong ul => checked((long)ul),
            string str => long.Parse(str),
            BigInteger bi => checked((long)bi),
            _ => ThrowValueConversionError(value)
        };

        public override bool CanParseValue(object value)
        {
            try
            {
                _ = ParseValue(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
