using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace Mapper
{
    public class ClassMapper
    {
        internal ClassMapper()
        {
        }

        internal static string MakeIdentifier(Type source, Type destination)
        {
            return $"{source.Name}||{destination.Name}";
        }
        internal class PropertyMap
        {
            internal PropertyInfo Source { get; set; }
            internal PropertyInfo Destination { get; set; }
            internal Expression MapExpression { get; set; }
        }

        internal interface IClassMap
        {
            void MapObjects(object source, object destination);
            void Compile();
        }

        private IClassMap GetMap(Type source, Type destination)
        {
            var id = MakeIdentifier(source, destination);
            if (_maps.ContainsKey(id))
            {
                var map = _maps[id];
                return map;
            }

            throw new MapperException($"Could not find map for types {source.Name}, {destination.Name}");
        }

        private readonly Dictionary<string, IClassMap> _maps = new();

        [Serializable]
        public class MapperException : Exception
        {
            public MapperException() {}

            public MapperException(string message)
                : base(message) {}

            public MapperException(string message, Exception innerException) : base(message, innerException) {}

            protected MapperException(SerializationInfo info, StreamingContext context) : base(info, context) {}
        }

        public class ClassMap<TSource, TDestination> : IClassMap
        {
            internal ClassMapper _parent;
            private readonly List<ParameterExpression> _parameters = new();
            private ParameterExpression SourceParameter => _parameters[0];
            private ParameterExpression DestinationParameter => _parameters[1];
            internal List<PropertyMap> PropertyMaps { get; set; }
            private Delegate MapFunction { get; set; }
            internal ClassMap(ClassMapper parent)
            {
                _parent = parent;
                _parameters.Add(Expression.Parameter(typeof(TSource)));
                _parameters.Add(Expression.Parameter(typeof(TDestination)));
                Identifier = MakeIdentifier(typeof(TSource), typeof(TDestination));
                PropertyMaps = new List<PropertyMap>();
            }

            internal void CreateMap()
            {
                const BindingFlags sourcePropertyFlags = BindingFlags.Public | BindingFlags.Instance;

                foreach (var sourceProperty in typeof(TSource).GetProperties(sourcePropertyFlags))
                {
                    var destinationProperty = typeof(TDestination).GetProperty(sourceProperty.Name);
                    if (destinationProperty == null || !destinationProperty.CanWrite)
                        continue;

                    if (destinationProperty.PropertyType != sourceProperty.PropertyType)
                    {
                        try //just try to force it to work
                        {
                            BuildCastMethod(sourceProperty, destinationProperty, destinationProperty.PropertyType);
                        }
                        catch (Exception ex)
                        {
                            throw new MapperException($"Could not build automatic cast between {destinationProperty.Name} and {sourceProperty.Name}.  Error was {ex.Message}", ex);
                        }
                    }
                    else
                    {
                        BuildCopyMethod(sourceProperty, destinationProperty);
                    }
                }
            }

            private void BuildCopyMethod(PropertyInfo sourceProperty, PropertyInfo destinationProperty)
            {
                var propertyGetExpression = Expression.Property(SourceParameter, sourceProperty.Name);
                var propertySetExpression = Expression.Property(DestinationParameter, destinationProperty.Name);
                var assignmentExpression = Expression.Assign(propertySetExpression, propertyGetExpression);

                PropertyMaps.Add(new PropertyMap
                {
                    Source = sourceProperty,
                    Destination = destinationProperty,
                    MapExpression = assignmentExpression,
                });
            }

            private void BuildCastMethod(PropertyInfo sourceProperty, PropertyInfo destinationProperty, Type destinationBaseType)
            {
                var propertyGetExpression = Expression.Property(SourceParameter, sourceProperty.Name);
                var cast = Expression.Convert(propertyGetExpression, destinationBaseType);
                var propertySetExpression = Expression.Property(DestinationParameter, destinationProperty.Name);
                var assignmentExpression = Expression.Assign(propertySetExpression, cast);

                PropertyMaps.Add(new PropertyMap
                {
                    Source = sourceProperty,
                    Destination = destinationProperty,
                    MapExpression = assignmentExpression,
                });
            }

            private static PropertyInfo GetPropertyInfo(MemberInfo member)
            {                
                return member.DeclaringType?.GetProperty(member.Name);
            }

            private static MemberExpression GetMemberExpression<T>(Expression<Func<T, object>> source)
            {
                if (source.Body is UnaryExpression { Operand: MemberExpression memberExpression })
                    return memberExpression;

                if (source.Body is MemberExpression bodyExpression)
                    return bodyExpression;

                throw new MapperException($"Could not find member for expression {source.Body}");
            }

            public string Identifier { get; internal set; }

            public void MapObjects(object source, object destination)
            {
                var args = _parameters.Select(p => p.Type == typeof(TDestination) ? destination : source).ToArray();
                MapFunction.DynamicInvoke(args);
            }

            /// <summary>
            /// Maps a destination property to a source property
            /// Even though you can use an expression to define the source,
            /// you can only specify the property directly
            /// Syntax:
            /// map.MapProperty(p => (int)p.OrderId), s=>s.OrderId)
            /// </summary>
            /// <returns>The ClassMap, for fluid syntax</returns>
            public ClassMap<TSource, TDestination> MapProperty(Expression<Func<TDestination, object>> destination, Expression<Func<TSource, object>> source)
            {
                var destExp = GetMemberExpression(destination);
                var existingMap = PropertyMaps.FirstOrDefault(m => m.Destination.Name == destExp.Member.Name);
                if (existingMap == null)
                {
                    existingMap = new PropertyMap();
                    PropertyMaps.Add(existingMap);
                }

                var propertySetExpression = Expression.Property(DestinationParameter, destExp.Member.Name);
                var exprConv = Expression.Convert(source.Body, typeof(string));
                var assignmentExpression = Expression.Assign(propertySetExpression, exprConv);

                _parameters.Add(source.Parameters[0]);
                existingMap.Destination = GetPropertyInfo(destExp.Member);
                existingMap.MapExpression = assignmentExpression;
                return this;
            }

            /// <summary>
            /// Creates the default map for the source and destination swapped around
            /// </summary>
            /// <returns>The new ClassMap</returns>
            public ClassMap<TDestination, TSource> ReverseMap()
            {
                var map = new ClassMap<TDestination, TSource>(_parent);
                map.CreateMap();
                _parent._maps.Add(map.Identifier, map);
                return map;
            }

            /// <summary>
            /// Compiles the mapping function
            /// This should be called after the mapping is complete, and before invoking the map
            /// </summary>
            public void Compile()
            {
                try
                {
                    var combinedExpression = Expression.Block(PropertyMaps.Select(p => p.MapExpression));
                    MapFunction = Expression.Lambda(combinedExpression, _parameters).Compile();
                }
                catch (Exception ex)
                {
                    throw new MapperException($"Error compiling map, error was {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// This creates the default map between the types
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TDestination"></typeparam>
        /// <returns>The ClassMap</returns>
        public ClassMap<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var map = new ClassMap<TSource, TDestination>(this);
            map.CreateMap();
            _maps.Add(map.Identifier, map);
            return map;
        }

        /// <summary>
        /// Copies the data from source to destination
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public void As(object source, object destination)
        {
            var sourceType = source.GetType();
            var destinationType = destination.GetType();
            var map = GetMap(sourceType, destinationType);
            map.MapObjects(source, destination);
        }

        /// <summary>
        /// Creates a new instance of the destination and copies the data from source into it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns>The new object</returns>
        public T To<T>(object source) where T : class, new()
        {
            var destination = new T();
            As(source, destination);
            return destination;
        }

        /// <summary>
        /// Compiles all the maps in the store
        /// </summary>
        public void Compile()
        {
            foreach (var classMap in _maps.Values)
            {
                classMap.Compile();
            }
        }
    }
}
