namespace Mapper
{
    public static class AutoMapper
    {
        private static ClassMapper _instance;

        /// <summary>
        /// The singleton instance of the ClassMapper 
        /// </summary>
        public static ClassMapper Instance => _instance ??= new ClassMapper();

        /// <summary>
        /// Creates a new instance of the destination and copies the data from source into it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T To<T>(this object source) where T : class, new()
        {
            return Instance.To<T>(source);
        }

        /// <summary>
        /// Creates the default map for the source and destination
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TDestination"></typeparam>
        /// <returns>The new ClassMap</returns>
        public static ClassMapper.ClassMap<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            return Instance.CreateMap<TSource, TDestination>();
        }

        /// <summary>
        /// Compiles the mapping function
        /// This should be called after the mapping is complete, and before invoking the map
        /// </summary>
        public static void Compile()
        {
            Instance.Compile();
        }
    }
}
