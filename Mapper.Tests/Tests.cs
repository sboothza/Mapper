using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapper.Tests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestBasic()
        {
            AutoMapper.CreateMap<TestModel, TestView>()
                .MapProperty(d => d.FirstName, s => s.Name)
                .MapProperty(d => d.LastName, s => s.Surname)
                .MapProperty(d => d.FullName, s => $"{s.Name} {s.Surname}")
                .ReverseMap()
                .MapProperty(d => d.Name, s => s.FirstName)
                .MapProperty(d => d.Surname, s => s.LastName)
                .Compile();

            var model = new TestModel
            {
                CustomerId = 1,
                DateOfBirth = DateTime.Now,
                Name = "Stephen",
                Surname = "Booth",
                NullableValue1 = 12,
                Value1 = 13
            };

            Console.WriteLine(model);
            var view = model.To<TestView>();
            Console.WriteLine(view);
            var model2 = view.To<TestModel>();
            Console.WriteLine(model2);
        }

        [TestMethod]
        public void TestPropertySet()
        {
            var model = new TestModel
            {
                CustomerId = 1,
                DateOfBirth = DateTime.Now,
                Name = "Stephen",
                Surname = "Booth"
            };

            var view = new TestView();

            var source = Expression.Parameter(typeof(TestModel));
            var destination = Expression.Parameter(typeof(TestView));
            var propertyGetExpression = Expression.Property(source, "Name");
            var propertySetExpression = Expression.Property(destination, "LastName");
            var assignmentExpression = Expression.Assign(propertySetExpression, propertyGetExpression);
            var lam = Expression.Lambda(assignmentExpression, source, destination).Compile();
            var result = lam.DynamicInvoke(model, view);
            Console.WriteLine(result);
            Console.WriteLine(model);
            Console.WriteLine(view);
        }

        [TestMethod]
        public void TestPropertySetMultiple()
        {
            var model = new TestModel
            {
                CustomerId = 1,
                DateOfBirth = DateTime.Now,
                Name = "Stephen",
                Surname = "Booth"
            };

            var view = new TestView();

            var parameters = new List<ParameterExpression>();

            parameters.Add(Expression.Parameter(typeof(TestModel)));
            parameters.Add(Expression.Parameter(typeof(TestView)));
            var propertyGetExpression = Expression.Property(parameters[0], "Name");
            var propertySetExpression = Expression.Property(parameters[1], "FirstName");
            var assignmentExpression = Expression.Assign(propertySetExpression, propertyGetExpression);

            var propertyGetExpression2 = Expression.Property(parameters[0], "Surname");
            var propertySetExpression2 = Expression.Property(parameters[1], "LastName");
            var assignmentExpression2 = Expression.Assign(propertySetExpression2, propertyGetExpression2);

            var propertySetExpression3 = Expression.Property(parameters[1], "FullName");
            Expression<Func<TestModel, object>> expr = s => $"{s.Name} {s.Surname}";
            parameters.Add(expr.Parameters[0]);
            var exprConv = Expression.Convert(expr.Body, typeof(string));
            var assignmentExpression3 = Expression.Assign(propertySetExpression3, exprConv);

            var combinedExpression = Expression.Block(assignmentExpression, assignmentExpression2, assignmentExpression3);
            var lam = Expression.Lambda(combinedExpression, parameters).Compile();

            object[] args = parameters.Select<ParameterExpression, object>(p =>
           {
               if (p.Type == typeof(TestModel))
                   return model;
               return view;
           }).ToArray();


            var result = lam.DynamicInvoke(args);
            Console.WriteLine(result);
            Console.WriteLine(model);
            Console.WriteLine(view);
        }

    }

    public class TestModel
    {
        public string Name { get; set; }
        public string Surname { get; set; }
        public DateTime DateOfBirth { get; set; }
        public int CustomerId { get; set; }
        public long Value1 { get; set; }
        public long? NullableValue1 { get; set; }

        public override string ToString()
        {
            return $"id:{CustomerId} name:{Name} surname:{Surname} dob:{DateOfBirth:d}";
        }
    }

    public class TestView
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public int? CustomerId { get; set; }
        public string FullName { get; set; }
        public int Value1 { get; set; }
        public long? NullableValue1 { get; set; }

        public override string ToString()
        {
            return $"id:{CustomerId} name:{FirstName} surname:{LastName} dob:{DateOfBirth:d}";
        }
    }
}
