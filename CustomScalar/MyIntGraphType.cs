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


    public class MyLongGraphType : LongGraphType
    {
        public MyLongGraphType()
        {
            Name = "Long";
        }

        public override object ParseLiteral(IValue value) => value switch
        {
            StringValue s => ParseValue(s.Value),
            IntValue intValue => intValue.Value,
            LongValue longValue => longValue.Value,
            BigIntValue bigIntValue => checked((long)bigIntValue.Value),
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
                    StringValue str => long.TryParse(str.Value, out long @res),
                    LongValue longValue => long.MinValue <= longValue.Value && longValue.Value <= long.MaxValue,
                    BigIntValue bigIntValue => long.MinValue <= bigIntValue.Value && bigIntValue.Value <= long.MaxValue,
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

        public override IValue ToAST(object value) => Serialize(value) switch
        {
            long b => new LongValue(b),
            null => new NullValue(),
            _ => ThrowASTConversionError(value)
        };
    }
}
