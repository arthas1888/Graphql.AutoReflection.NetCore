using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace SER.Graphql.Reflection.NetCore.Utilities
{
    public static class  SqlCommandExtension
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
    }
}
