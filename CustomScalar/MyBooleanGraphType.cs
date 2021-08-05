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
    public class MyBooleanGraphType : BooleanGraphType
    {
        public MyBooleanGraphType()
        {
            Name = "Boolean";
        }

        public override object ParseLiteral(IValue value) => value switch
        {
            BooleanValue b => b.Value,
            IntValue i => ParseValue(i.Value),
            LongValue l => ParseValue(l.Value),
            BigIntValue bi => ParseValue(bi.Value),
            StringValue s => ParseValue(s.Value),
            FloatValue f => ParseValue(f.Value),
            DecimalValue d => ParseValue(d.Value),
            NullValue _ => null,
            _ => ThrowLiteralConversionError(value)
        };

        public override bool CanParseLiteral(IValue value)
        {
            try
            {
                _ = ParseLiteral(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override object ParseValue(object value) => value switch
        {
            bool _ => value,
            byte b => b != 0,
            sbyte sb => sb != 0,
            short s => s != 0,
            ushort us => us != 0,
            int i => i != 0,
            uint ui => ui != 0,
            long l => l != 0,
            ulong ul => ul != 0,
            BigInteger bi => bi != 0,
            float f => f != 0,
            double d => d != 0,
            decimal d => d != 0,
            string s => bool.Parse(s),
            null => null,
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
            bool b => new BooleanValue(b),
            null => new NullValue(),
            _ => ThrowASTConversionError(value)
        };
    }
}
