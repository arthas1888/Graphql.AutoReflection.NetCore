using GraphQL.Language.AST;
using GraphQL.Types;
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

        public override object ParseLiteral(IValue value) => value switch
        {
            StringValue s => ParseValue(s.Value),
            IntValue intValue => intValue.Value,
            LongValue longValue => checked((int)longValue.Value),
            BigIntValue bigIntValue => checked((int)bigIntValue.Value),
            NullValue _ => null,
            _ => ThrowLiteralConversionError(value)
        };

        public override bool CanParseLiteral(IValue value)
        {
            try
            {
                _ = ParseLiteral(value);
                return (value) switch
                {
                    IntValue _ => true,
                    StringValue str => int.TryParse(str.Value, out int @res),
                    LongValue longValue => int.MinValue <= longValue.Value && longValue.Value <= int.MaxValue,
                    BigIntValue bigIntValue => int.MinValue <= bigIntValue.Value && bigIntValue.Value <= int.MaxValue,
                    NullValue _ => true,
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

        public override IValue ToAST(object value) => Serialize(value) switch
        {
            int b => new IntValue(b),
            null => new NullValue(),
            _ => ThrowASTConversionError(value)
        };
    }
}
