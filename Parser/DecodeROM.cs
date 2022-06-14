using GraphQLParser.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.Parser
{
    public static class DecodeROM
    {
        /// <summary>
        /// convierte un objeto graphqlvalue al tipo de objeto original
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object ParseLiteral(this GraphQLValue value) => value switch
        {
            GraphQLIntValue x => TryParseNumber(x.Value),
            GraphQLFloatValue x => TryParseFloat(x.Value),
            GraphQLBooleanValue x => TryParseBoolean(x.Value),
            GraphQLStringValue x => x.Value.ToString(),
            GraphQLNullValue _ => null,
            _ => null
        };


        /// <summary>
        /// obtiene el valor verdadero de un campo tipo ROM
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object GetRealValue(this object value) =>
            value == null ? null :
            value is string ? value as string :
            value is DateTime ? value as DateTime? :
            value is Guid ? value as Guid? :
            value is GraphQLParser.ROM ? ParseLiteral((GraphQLParser.ROM)value) : value;


        /// <summary>
        /// convierte un objeto ROM al tipo original
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static object ParseLiteral(this GraphQLParser.ROM value)
        {
            if (value == "null") return null;
            if (value == null) return null;
            if (value == "true" || value == "false") return TryParseBoolean(value);
            try
            {
                return TryParseNumber(value);
            }
            catch (Exception)
            {
                try
                {
                    return TryParseFloat(value);
                }
                catch (Exception)
                {
                    return value.ToString();
                }
            }

        }

        /// <summary>
        /// vetifica si el input es un booleano
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static bool TryParseBoolean(ReadOnlySpan<char> input)
        {
            if (bool.TryParse(input, out var @bool))
                return @bool;
            throw new Exception("Invalid boolean value");
        }

        /// <summary>
        /// verifica si la cadena de caracteres se puede parsear a un flotante
        /// </summary>
        /// <param name="chars"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static object TryParseFloat(ReadOnlySpan<char> chars)
        {
            if (double.TryParse(chars, out var number) /* && !double.IsNaN(number) */ && !double.IsInfinity(number))
                return (double)number;
            if (float.TryParse(chars, out var @float) && !float.IsInfinity(@float))
                return (float)@float;
            if (decimal.TryParse(chars, out var @decimal))
                return (decimal)@decimal;
            throw new Exception("Invalid float value");
        }
        
        /// <summary>
        /// verifica si la cadena de caracteres se puede parsear a un int
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static object TryParseNumber(ReadOnlySpan<char> input)
        {
            if (int.TryParse(input, out int int32) && int32 <= int.MaxValue)
                return (int)int32;
            else if (long.TryParse(input, out long int64) && int64 <= long.MaxValue)
                return (long)int64;
            throw new Exception("Invalid int value");
        }
    }
}
