using SER.Graphql.Reflection.NetCore.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace SER.Graphql.Reflection.NetCore.Utilities
{
    public static class  SqlCommandExt
    {
        public static bool ConcatFilter(List<object> values, StringBuilder expresion, string paramName,
            string key, string value, string column, Type typeProperty = null, int? index = null, bool isList = false,
            bool isValid = false)
        {
            var select = "";
            var enable = true;
            var expValided = false;
            var patternStr = @"\=|¬";
            if (typeProperty != null)
            {
                if (typeProperty == typeof(string))
                {
                    expValided = true;
                    values.Add(value.ToLower());
                    select = string.Format(".ToLower().Contains({0})", paramName);
                }
            }
            else
            {
                expValided = true;
                if (int.TryParse(value, out int number))
                {
                    values.Add(number);
                    select = string.Format(" = {0}", paramName);
                }
                else if (bool.TryParse(value, out bool boolean))
                {
                    values.Add(boolean);
                    select = string.Format(" = {0}", paramName);
                }
                else if (float.TryParse(value, out float fnumber))
                {
                    values.Add(fnumber);
                    select = string.Format(" = {0}", paramName);
                }
                else if (double.TryParse(value, out double dnumber))
                {
                    values.Add(dnumber);
                    select = string.Format(" = {0}", paramName);
                }
                else if (decimal.TryParse(value, out decimal denumber))
                {
                    values.Add(denumber);
                    select = string.Format(" = {0}", paramName);
                }
                else if (DateTime.TryParse(value, out DateTime dateTime) == true)
                {
                    values.Add(dateTime.Date);
                    select = string.Format(" = {0}", paramName);
                }
                else
                {
                    if (typeProperty != null && typeProperty != typeof(string))
                    {
                        enable = false;
                    }
                    Match matchStr = Regex.Match(column, patternStr);
                    if (matchStr.Success)
                    {
                        if (matchStr.Value == "=")
                        {
                            values.Add(value);
                            select = string.Format(" = {0}", paramName);
                        }
                        else
                        {
                            values.Add(value.ToLower());
                            select = string.Format(".ToLower().Contains({0})", paramName);
                        }
                    }

                }
            }

            if (enable)
            {
                if (index != null && index > 0 && expresion.Length > 3 && isValid && expValided)
                {
                    if (isList)
                        expresion.Append(")");
                    expresion.Append(" OR ");
                }

                if (expValided)
                {
                    expresion.Append(key);
                    expresion.Append(select);
                }

            }
            return expValided;
        }

        public static void ConcatFilter<T>(List<Expression<Func<T, bool>>> listExpOR, List<Expression<Func<T, bool>>> listExpAND,
          int index, string key, object value, string patternToEvaluate, Type fieldType, Match match = null) where T : class
        {
            string select = "";
            Expression<Func<T, bool>> expToEvaluate = null;

            if (patternToEvaluate == "¬")
            {
                if (fieldType == typeof(string))
                {
                    // expToEvaluate = FilterILike<T>(key, $"%{value}%");
                    expToEvaluate = (b => EF.Functions.ILike(EF.Property<string>(b, key), $"%{value}%"));
                }
                else if (fieldType == typeof(IBaseModel))
                {
                    select = string.Format("{0}.ToLower().Contains(@{1})", key, 0);
                    expToEvaluate = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, select, ((string)value).ToLower());

                }
                else if (TypeExtensions.IsNumber(fieldType))
                {
                    select = string.Format("string(object({0})).Contains(@{1})", key, 0);
                    expToEvaluate = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, select, value);
                }

            }
            else
            {
                if (value is DateTime || value is DateTime)
                {
                }
                else
                {
                    select = string.Format("{0} = @{1}", key, 0);
                    expToEvaluate = DynamicExpressionParser.ParseLambda<T, bool>(new ParsingConfig(), true, select, value);
                }
            }

            if (match == null || index == 0) { if (expToEvaluate != null) listExpOR.Add(expToEvaluate); }
            else
            {
                // query filtro por AND o OR  
                if (index > 1)
                    match = match.NextMatch();

                if (match.Success)
                {
                    if (match.Value == "/")
                    {
                        if (expToEvaluate != null) listExpAND.Add(expToEvaluate);
                    }
                    else
                    {
                        if (expToEvaluate != null) listExpOR.Add(expToEvaluate);
                    }
                }
            }

        }

    }
}
