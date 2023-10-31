using SER.Graphql.Reflection.NetCore.Generic;
using GraphQL;
using GraphQL.Types;
using GraphQL.Utilities;
using System;
using NetTopologySuite.Geometries;
using SER.Graphql.Reflection.NetCore.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SER.Graphql.Reflection.NetCore.CustomScalar;

namespace SER.Graphql.Reflection.NetCore
{
    public class AppSchema<TUser, TRole, TUserRole> : Schema
        where TUser : class
        where TRole : class
        where TUserRole : class
    {
        public AppSchema(IServiceProvider services)
            : base(services)
        {
            ValueConverter.Register(typeof(string), typeof(Point), ParsePoint);
            ValueConverter.Register(typeof(string), typeof(Coordinate), ParseCoordinate);
            ValueConverter.Register(typeof(string), typeof(LineString), ParseLineString);
            ValueConverter.Register(typeof(string), typeof(Polygon), ParsePolygonString);
            ValueConverter.Register(typeof(string), typeof(MultiLineString), ParseMultiLineString);
            ValueConverter.Register(typeof(string), typeof(TimeSpan), TimeSpanConvert);
            ValueConverter.Register(typeof(string), typeof(int), IntConvert);
            ValueConverter.Register(typeof(string), typeof(DateTime), DatetimeConvert);
            ValueConverter.Register(typeof(string), typeof(bool), BoolConvert);

            Query = services.GetRequiredService<GraphQLQuery<TUser, TRole, TUserRole>>();
            Mutation = services.GetRequiredService<AppMutation>();
            //Subscription = services.GetRequiredService<AppSubscriptions>();

            RegisterType(new MyBooleanGraphType());
            RegisterType(new MyIntGraphType());
            RegisterType(new MyLongGraphType());
            RegisterType(new MyDateTimeGraphType());
        }
        
        private object BoolConvert(object value)
        {
            try
            {
                var input = (string)value;
                return bool.Parse(input);
            }
            catch
            {
                throw new FormatException($"Failed to parse bool from input '{value}'. Input should be a string of bool representation");
            }
        }

        private object IntConvert(object value)
        {
            try
            {
                var input = (string)value;
                return int.Parse(input);
            }
            catch
            {
                throw new FormatException($"Failed to parse int from input '{value}'. Input should be a string of int representation");
            }
        }

        private object DatetimeConvert(object value)
        {
            try
            {
                var input = (string)value;
                return DateTime.Parse(input);
            }
            catch
            {
                throw new FormatException($"Failed to parse {nameof(DateTime)} from input '{value}'. Input should be a string of DateTime representation");
            }
        }

        private object TimeSpanConvert(object value)
        {
            try
            {
                var input = (string)value;
                return TimeSpan.Parse(input);
            }
            catch
            {
                throw new FormatException($"Failed to parse {nameof(TimeSpan)} from input '{value}'. Input should be a string of timespan representation");
            }
        }

        private object ParsePoint(object geometryInpunt)
        {
            try
            {
                var geometryString = (string)geometryInpunt;
                return JsonExtensions.DeserializeWithGeoJson<Point>(geometryString);
            }
            catch
            {
                throw new FormatException($"Failed to parse {nameof(Point)} from input '{geometryInpunt}'. Input should be a string of geojson representation");
            }
        }

        private object ParseCoordinate(object geometryInpunt)
        {
            try
            {
                var geometryString = (string)geometryInpunt;
                return JsonExtensions.DeserializeWithGeoJson<Coordinate>(geometryString);
            }
            catch
            {
                throw new FormatException($"Failed to parse {nameof(Coordinate)} from input '{geometryInpunt}'. Input should be a string of geojson representation");
            }
        }

        private object ParseLineString(object geometryInpunt)
        {
            try
            {
                var geometryString = (string)geometryInpunt;
                return JsonExtensions.DeserializeWithGeoJson<LineString>(geometryString);
            }
            catch
            {
                throw new FormatException($"Failed to parse {nameof(LineString)} from input '{geometryInpunt}'. Input should be a string of geojson representation");
            }
        }

        private object ParsePolygonString(object geometryInpunt)
        {
            try
            {
                var geometryString = (string)geometryInpunt;
                return JsonExtensions.DeserializeWithGeoJson<Polygon>(geometryString);
            }
            catch
            {
                throw new FormatException($"Failed to parse {nameof(Polygon)} from input '{geometryInpunt}'. Input should be a string of geojson representation");
            }
        }

        private object ParseMultiLineString(object geometryInpunt)
        {
            try
            {
                var geometryString = (string)geometryInpunt;
                return JsonExtensions.DeserializeWithGeoJson<MultiLineString>(geometryString);
            }
            catch
            {
                throw new FormatException($"Failed to parse {nameof(MultiLineString)} from input '{geometryInpunt}'. Input should be a string of geojson representation");
            }
        }

    }
}