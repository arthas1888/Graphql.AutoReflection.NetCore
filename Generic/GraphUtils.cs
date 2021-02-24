using SER.Graphql.Reflection.NetCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using GraphQL;
using GraphQL.Language.AST;
using GraphQL.Types;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace SER.Graphql.Reflection.NetCore.Generic
{
    public static class GraphUtils
    {
        public static int FilterAllFields(Type type, List<object> args, StringBuilder whereArgs, int i, string value,
            string parentModel = "", bool isList = false)
        {
            if (whereArgs.Length > 0) whereArgs.Append(" AND ");

            var lastValid = false;
            whereArgs.Append("( ");
            foreach (var (propertyInfo, j) in type.GetProperties().Select((v, j) => (v, j)))
            {
                if (!propertyInfo.GetCustomAttributes(true).Any(x => x.GetType() == typeof(JsonIgnoreAttribute))
                    && !propertyInfo.GetCustomAttributes(true).Any(x => x.GetType() == typeof(NotMappedAttribute))
                    && !propertyInfo.PropertyType.Name.Contains("List"))
                {
                    var key = propertyInfo.Name;

                    if (!string.IsNullOrEmpty(parentModel))
                        key = $"{parentModel}.{propertyInfo.Name}";
                    if (isList)
                        key = $"{parentModel}.Any({propertyInfo.Name}";
                    var isValid = SqlCommandExtension.ConcatFilter(args, whereArgs, $"@{i}", key, value, "¬",
                        typeProperty: propertyInfo.PropertyType, index: j, isList: isList, isValid: lastValid);

                    if (isValid)
                    {
                        lastValid = true;
                        i++;
                    }
                }
            }
            if (isList)
                whereArgs.Append(")");
            whereArgs.Append(" )");

            return i;
        }

        private static dynamic FilterILike(Type type, string propertyName, object value)
        {
            var lambdaParam = Expression.Parameter(type);
            var property = Expression.Property(lambdaParam, propertyName);
            var expr = Expression.Call(
                           typeof(NpgsqlDbFunctionsExtensions),
                           nameof(NpgsqlDbFunctionsExtensions.ILike),
                           Type.EmptyTypes,
                           Expression.Property(null, typeof(EF), nameof(EF.Functions)),
                           property,
                           Expression.Constant($"%{value}%"));

            return Expression.Lambda(expr, lambdaParam);
            //return Expression.Lambda<Func<Country, bool>>(expr, lambdaParam);
        }

        public static void ConcatFilter(List<object> values, StringBuilder expresion, int index,
          string key, object value, bool isList = false, Type type = null, Type modelType = null)
        {
            bool filterWithOr = false;
            bool appendKey = true;
            string select;
            var patternStr = @"_iext";
            Match matchStr = Regex.Match(key, patternStr);

            var patternOr = @"_iext_or";
            Match matchOr = Regex.Match(key, patternOr);

            var patternExtStr = @"_ext";
            Match matchExtStr = Regex.Match(key, patternExtStr);

            var patternIsNullStr = @"_isnull";
            Match matchIsNullStr = Regex.Match(key, patternIsNullStr);

            var patternExcludeStr = @"_exclude";
            Match matchExcludStr = Regex.Match(key, patternExcludeStr);

            var patternEnum = @"_enum";
            Match matchEnum = Regex.Match(key, patternEnum);
            if (matchEnum.Success) key = Regex.Replace(key, patternEnum, "");

            if (matchOr.Success) filterWithOr = true;
            if (matchStr.Success || filterWithOr)
            {
                if (filterWithOr) key = Regex.Replace(key, patternOr, "");
                else key = Regex.Replace(key, patternStr, "");

                var patternSeparate = @"¬";
                if (Regex.Match(value.ToString(), patternSeparate).Success)
                {
                    var arrayValues = Regex.Split(value.ToString(), patternSeparate);
                    if (Utilities.TypeExtensions.IsNumber(type) || type.IsEnum)
                    {
                        select = string.Format("@{0}.Contains(int({1}))", index, key);
                        values.Add(Array.ConvertAll(arrayValues, s => int.Parse(s)));
                    }
                    else
                    {
                        select = string.Format("@{0}.Contains({1})", index, key);
                        values.Add(arrayValues);
                    }

                    appendKey = false;
                }
                else
                {
                    if (Utilities.TypeExtensions.IsNumber(type) || type.IsEnum)
                    {
                        appendKey = false;
                        values.Add(value.ToString());
                        select = string.Format("string(object({0})).Contains(@{1})", key, index);
                    }
                    else if (type == typeof(string))
                    {
                        if (key.Contains("."))
                        {
                            values.Add(((string)value).ToLower());
                            select = string.Format(".ToLower().Contains(@{0})", index);
                        }
                        else
                        {
                            var expTo = FilterILike(modelType, key, value);
                            //Expression<Func<T, bool>> expToEvaluate = (b => EF.Functions.ILike(EF.Property<string>(b, key), $"%{value}%"));
                            select = string.Format("@{0}(it)", index);
                            values.Add(expTo);
                            appendKey = false;
                        }

                    }
                    else
                    {
                        values.Add(value);
                        select = string.Format(" = @{0}", index);
                    }
                }

            }
            else if (matchExtStr.Success)
            {
                key = Regex.Replace(key, patternExtStr, "");
                values.Add(value);
                select = string.Format(".Contains(@{0})", index);
            }
            else if (matchIsNullStr.Success)
            {
                key = Regex.Replace(key, patternIsNullStr, "");
                values.Add(null);
                select = string.Format(" {0} @{1}", ((bool)value) ? "==" : "!=", index);
            }
            else if (matchExcludStr.Success)
            {
                key = Regex.Replace(key, patternExcludeStr, "");
                values.Add(value);
                select = string.Format(" != @{0}", index);
            }
            else
            {
                if (value is DateTime || value is int)
                {
                    var symbol = " = ";
                    if (Regex.Match(key, "_gte").Success) { symbol = " >= "; patternStr = "_gte"; }
                    else if (Regex.Match(key, "_gt").Success) { symbol = " > "; patternStr = "_gt"; }
                    else if (Regex.Match(key, "_lte").Success) { symbol = " <= "; patternStr = "_lte"; }
                    else if (Regex.Match(key, "_lt").Success) { symbol = " < "; patternStr = "_lt"; }

                    key = Regex.Replace(key, patternStr, "");

                    values.Add(value);
                    select = string.Format(" {0} @{1}", symbol, index);
                }
                else
                {
                    values.Add(value);
                    select = string.Format(" = @{0}", index);
                }
            }

            if (expresion.Length > 0)
                if (filterWithOr)
                    expresion.Append(" OR ");
                else expresion.Append(" AND ");

            if (appendKey)
                expresion.Append(key);

            expresion.Append(select);
            if (isList)
                expresion.Append(")");
        }

        public static void DetectChild<TUser, TRole, TUserRole>(IList<ISelection> selections, List<string> includes, dynamic resolvedType, List<object> args,
            StringBuilder whereArgs, string mainModel = "", IDictionary<string, object> arguments = null, Type mainType = null)
            where TUser : class
            where TRole : class
            where TUserRole : class
        {
            var model = string.Empty;
            Type innerType = null;
            bool isList = false;
            bool joinList = false;
            FieldType fieldType = null;
            var i = 0;

            if (mainModel == "" && arguments != null)
            {
              
                var type = mainType;
                foreach (var argument in arguments)
                {
                    if (argument.Value != null && argument.Key != null)
                    {
                        if (new string[] { "orderBy", "first", "page", "join" }.Contains(argument.Key)) continue;

                        if (argument.Key == "all")
                        {
                            i = FilterAllFields(type, args, whereArgs, i, argument.Value.ToString());
                        }
                        else
                        {
                            FilterArguments<TUser, TRole, TUserRole>(argument.Key, argument.Value, type, i, args, whereArgs);
                            i++;
                        }
                    }
                }
            }
            if (selections != null)
            {
                foreach (Field field in selections)
                {
                    //Console.WriteLine($"name {field.Name}");
                    if (field.SelectionSet.Selections.Count > 0)
                    {
                        model = field.Name;
                        if ((mainType == typeof(TUser) && typeof(IdentityUser).IsAssignableFrom(typeof(TUser)))
                            || (mainType == typeof(TRole) && typeof(IdentityRole).IsAssignableFrom(typeof(TRole)))
                            || (mainType == typeof(TUserRole))
                            )
                            model = FirstLetterToUpper(model);

                        try
                        {
                            fieldType = ((IEnumerable<FieldType>)resolvedType.Fields).SingleOrDefault(x => x.Name == field.Name);
                        }
                        catch (Exception) { }
                        if (fieldType != null)
                        {
                            // detect if field is List
                            if (fieldType.ResolvedType.GetType().IsGenericType && fieldType.ResolvedType is ListGraphType)
                            {
                                innerType = ((dynamic)fieldType.ResolvedType).ResolvedType.GetType().GenericTypeArguments.Length > 0 ?
                                            ((dynamic)fieldType.ResolvedType).ResolvedType.GetType().GetGenericArguments()[0] : null;
                                isList = true;
                            }
                            // detect if field is object
                            else if (fieldType.ResolvedType.GetType().IsGenericType && fieldType.ResolvedType.GetType().GetGenericTypeDefinition() == typeof(ObjectGraphType<>))
                            {
                                innerType = fieldType.ResolvedType.GetType().GenericTypeArguments.Length > 0 ? fieldType.ResolvedType.GetType().GetGenericArguments()[0] : null;

                                if (!string.IsNullOrEmpty(mainModel))
                                    model = $"{mainModel}.{model}";
                                includes.Add(model);
                            }
                        }

                        if (field.Arguments != null && !isList)
                        {
                            foreach (var argument in field.Arguments)
                            {
                                if (new string[] { "orderby", "first", "page", "join" }.Contains(argument.Name)) continue;

                                var headerModel = model;
                                if (!string.IsNullOrEmpty(mainModel))
                                    headerModel = $"{mainModel}.{model}";
                                if (argument.Name == "all")
                                {
                                    if (innerType != null)
                                        i = FilterAllFields(innerType, args, whereArgs, i, argument.Value.Value.ToString(), parentModel: headerModel);
                                }
                                else
                                {
                                    if (innerType != null)
                                        FilterArguments<TUser, TRole, TUserRole>(argument.Name, argument.Value.Value, innerType, i, args, whereArgs, alias: headerModel);
                                    i++;
                                }
                            }
                        }
                        else if (field.Arguments != null && field.Arguments.Count() > 0)
                        {
                            if (field.Arguments.Any(x => x.Name == "join" && (bool)x.Value.Value == true))
                            {
                                joinList = true;
                                includes.Add(model);

                                foreach (var argument in field.Arguments)
                                {
                                    if (new string[] { "orderBy", "first", "join" }.Contains(argument.Name)) continue;

                                    if (argument.Name == "all")
                                    {
                                        if (innerType != null)
                                            i = FilterAllFields(innerType, args, whereArgs, i, argument.Value.Value.ToString(),
                                            isList: true, parentModel: model);
                                    }
                                    else
                                    {
                                        if (innerType != null)
                                            FilterArguments<TUser, TRole, TUserRole>(argument.Name, argument.Value.Value, innerType, i, args, whereArgs,
                                                isList: true, alias: $"{model}.Any(");
                                        i++;
                                    }
                                }
                            }

                        }

                        var innerResolvedType = fieldType != null ? fieldType.ResolvedType : resolvedType;
                        if (joinList)
                        {
                            try
                            {
                                innerResolvedType = ((dynamic)fieldType.ResolvedType).ResolvedType;
                            }
                            catch (Exception) { }
                        }
                        DetectChild<TUser, TRole, TUserRole>(field.SelectionSet.Selections, includes, innerResolvedType, args, whereArgs, mainModel: model, mainType: innerType);
                    }
                }
            }
        }

        public static string FirstLetterToUpper(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }

        private static void FilterArguments<TUser, TRole, TUserRole>(string key, object value,
             Type type, int i, List<object> args, StringBuilder whereArgs, bool isList = false, string alias = null)
            where TUser : class
            where TRole : class
            where TUserRole : class
        {
            string keyName = key;
            if (key.Contains("__model__"))
            {
                type = FindChildType(type, keyName.Split("__model__")[0]);
                keyName = keyName.Replace("__model__", ".");
            }
            if (key.Contains("__list__"))
            {
                type = FindChildType(type, keyName.Split("__list__")[0]);
                keyName = keyName.Replace("__list__", ".Any(");
                isList = true;
            }

            Type fieldLocalType = null;
            var patternStr = @"_iext";
            Match matchStr = Regex.Match(keyName, patternStr);

            var patternOr = @"_iext_or";
            Match matchOr = Regex.Match(keyName, patternOr);

            var patternExtStr = @"_ext";
            Match matchExtStr = Regex.Match(keyName, patternExtStr);

            var patternIsNullStr = @"_isnull";
            Match matchIsNulltStr = Regex.Match(keyName, patternIsNullStr);

            var patternExcludeStr = @"_exclude";
            Match matchExcludStr = Regex.Match(key, patternExcludeStr);

            var patternEnum = @"_enum";
            Match matchIsEnum = Regex.Match(keyName, patternEnum);

            if (matchStr.Success || matchExtStr.Success || matchIsNulltStr.Success || matchOr.Success || matchIsEnum.Success || matchExcludStr.Success)
            {
                var fieldName = "";

                if (matchOr.Success)
                    fieldName = Regex.Replace(keyName, patternOr, "");
                else if (matchStr.Success)
                    fieldName = Regex.Replace(keyName, patternStr, "");
                else fieldName = keyName;

                if (matchExtStr.Success)
                    fieldName = Regex.Replace(fieldName, patternExtStr, "");
                if (matchIsNulltStr.Success)
                    fieldName = Regex.Replace(fieldName, patternIsNullStr, "");
                if (matchIsEnum.Success)
                    fieldName = Regex.Replace(fieldName, patternEnum, "");
                if (matchExcludStr.Success)
                    fieldName = Regex.Replace(fieldName, patternExcludeStr, "");

                if ((type == typeof(TUser) && typeof(IdentityUser).IsAssignableFrom(typeof(TUser)))
                    || (type == typeof(TRole) && typeof(IdentityRole).IsAssignableFrom(typeof(TRole)))
                    || (type == typeof(TUserRole))
                    )
                    fieldName = FirstLetterToUpper(fieldName);

                if (key.Contains("__model__"))
                {
                    fieldName = fieldName.Split(".")[1];
                }
                if (key.Contains("__list__"))
                {
                    fieldName = fieldName.Split(".Any(")[1];
                }

                foreach (var (propertyInfo, j) in type.GetProperties().Select((v, j) => (v, j)))
                {
                    if (propertyInfo.Name == fieldName)
                    {
                        fieldLocalType = propertyInfo.PropertyType;
                        if (fieldLocalType.IsGenericType && fieldLocalType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            fieldLocalType = fieldLocalType.GetGenericArguments()[0];
                        break;
                    }
                }
                //Console.WriteLine($"=================================type {type} name {keyName} fieldName {fieldName} isList {isList} fieldLocalType {fieldLocalType} =========================");
            }

            ConcatFilter(args, whereArgs, i, alias != null ? isList ? $"{alias}{keyName}" : $"{alias}.{keyName}" : keyName, value,
                type: fieldLocalType, isList: isList, modelType: type);
        }

        private static Type FindChildType(Type type, string key)
        {
            Type fieldLocalType = null;
            foreach (var (propertyInfo, j) in type.GetProperties().Select((v, j) => (v, j)))
            {
                if (propertyInfo.Name == key)
                {
                    fieldLocalType = propertyInfo.PropertyType;
                    if (fieldLocalType.IsGenericType && fieldLocalType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        fieldLocalType = fieldLocalType.GetGenericArguments()[0];
                    break;
                }
            }
            return fieldLocalType;
        }

        public static Type ResolveGraphType(Type type)
        {
            try
            {
                if (type == typeof(DateTime))
                    return typeof(DateTimeGraphType);

                return type.GetGraphTypeFromType(true);
            }
            catch (Exception)
            {
                return typeof(string).GetGraphTypeFromType(true);
            }
        }
    }

}
