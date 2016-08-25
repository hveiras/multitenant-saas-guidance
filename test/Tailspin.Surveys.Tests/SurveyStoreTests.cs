﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Tailspin.Surveys.Data.DataModels;
using Microsoft.EntityFrameworkCore;
using Tailspin.Surveys.Data.DataStore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Tailspin.Surveys.Tests
{
    // This test uses the InMemoryDatabase to simulate the SQL database.
    // Be aware that an in-memory DB will never exactly match the behavior of the real DB.

    // When using InMemoryDatabase for unit testing, create a new database for each test,
    // and don't re-use the same DbContext to populate the database and run the queries.

    public class SurveyStoreTests
    {
        // FROM: https://docs.efproject.net/en/latest/miscellaneous/testing.html Revision 50e38b00
        private static DbContextOptions<ApplicationDbContext> CreateNewContextOptions()
        {
            // Create a fresh service provider, and therefore a fresh 
            // InMemory database instance.
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            // Create a new options instance telling the context to use an
            // InMemory database and the new service provider.
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            builder.UseInMemoryDatabase()
                   .UseInternalServiceProvider(serviceProvider);

            return builder.Options;
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_CorrectSurvey()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();
            
            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                context.Add(new Survey { Id = 1 });
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetSurveyAsync(1);
                Assert.Equal(1, result.Id);
            }
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_Survey_Contributors()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                var survey = new Survey
                {
                    Id = 1,
                    Contributors = new List<SurveyContributor>
                    {
                        new SurveyContributor { SurveyId = 1, UserId = 2 }
                    }
                };
                context.Add(survey);
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetSurveyAsync(1);

                Assert.NotNull(result.Contributors);
                Assert.NotEmpty(result.Contributors);
            }
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_Survey_Questions()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                var survey = new Survey
                {
                    Id = 1,
                    Questions = new List<Question>
                    {
                        new Question { SurveyId = 1  }
                    }
                };
                context.Add(survey);
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetSurveyAsync(1);

                Assert.NotNull(result.Questions);
                Assert.NotEmpty(result.Questions);
            }
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_Survey_Requests()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                var survey = new Survey
                {
                    Id = 1,
                    Requests = new List<ContributorRequest>
                    {
                        new ContributorRequest()
                    }
                };
                context.Add(survey);
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetSurveyAsync(1);

                Assert.NotNull(result.Requests);
                Assert.NotEmpty(result.Requests);
            }
        }

        [Fact]
        public async Task GetSurveysByOwnerAsync_Returns_CorrectSurveys()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                context.AddRange(
                    new Survey { Id = 1, OwnerId = 1 },
                    new Survey { Id = 2, OwnerId = 1 },
                    new Survey { Id = 3, OwnerId = 2 }
                    );
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetSurveysByOwnerAsync(1);

                Assert.NotEmpty(result);
                // Returned collection should only contain surveys with the matching owner ID.
                Assert.True(result.All(s => s.OwnerId == 1));
            }
        }

        [Fact]
        public async Task GetPublishedSurveysByOwnerAsync_Returns_PublishedSurveys()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                context.AddRange(
                    new Survey { Id = 1, OwnerId = 1 },
                    new Survey { Id = 2, OwnerId = 1, Published = true },
                    new Survey { Id = 3, OwnerId = 1, Published = true },
                    new Survey { Id = 4, OwnerId = 2, Published = true }  
                    );
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetPublishedSurveysByOwnerAsync(1);

                Assert.Equal(2, result.Count);
                Assert.True(result.All(s => s.OwnerId == 1));  // must match owner ID
                Assert.True(result.All(s => s.Published == true)); // only published surveys
            }
        }

        [Fact]
        public async Task GetSurveysByContributorAsync_Returns_CorrectSurveys()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                context.AddRange(
                    new SurveyContributor { SurveyId = 1, UserId = 10 },
                    new SurveyContributor { SurveyId = 2, UserId = 10 },
                    new SurveyContributor { SurveyId = 3, UserId = 20 }
                    );
                context.AddRange(
                    new Survey { Id = 1, OwnerId = 1 },
                    new Survey { Id = 2, OwnerId = 2 },
                    new Survey { Id = 3, OwnerId = 3 },
                    new Survey { Id = 4, OwnerId = 4 }
                    );

                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetSurveysByContributorAsync(10);

                Assert.Equal(2, result.Count);
                Assert.Contains(result, s => s.Id == 1);
                Assert.Contains(result, s => s.Id == 2);
            }
        }

        [Fact]
        public async Task GetPublishedSurveysByTenantAsync_Returns_CorrectSurveys()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                context.AddRange(
                    new Survey { Id = 1, TenantId = 1 },
                    new Survey { Id = 2, TenantId = 1, Published = true },
                    new Survey { Id = 3, TenantId = 2 },
                    new Survey { Id = 4, TenantId = 2, Published = true }
                    );
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetPublishedSurveysByTenantAsync(1);

                Assert.Equal(1, result.Count);
                Assert.Equal(2, result.First().Id);
            }
        }

        [Fact]
        public async Task GetUnPublishedSurveysByTenantAsync_Returns_CorrectSurveys()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                context.AddRange(
                    new Survey { Id = 1, TenantId = 1 },
                    new Survey { Id = 2, TenantId = 1, Published = true },
                    new Survey { Id = 3, TenantId = 2 },
                    new Survey { Id = 4, TenantId = 2, Published = true }
                    );
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetUnPublishedSurveysByTenantAsync(1);

                Assert.Equal(1, result.Count);
                Assert.Equal(1, result.First().Id);
            }
        }

        [Fact]
        public async Task GetPublishedSurveysAsync_Returns_CorrectSurveys()
        {
            // All contexts that share the same service provider will share the same InMemory database
            var options = CreateNewContextOptions();

            // Run the test against one instance of the context
            using (var context = new ApplicationDbContext(options))
            {
                context.AddRange(
                    new Survey { Id = 1, TenantId = 1 },
                    new Survey { Id = 2, TenantId = 1, Published = true },
                    new Survey { Id = 3, TenantId = 2 },
                    new Survey { Id = 4, TenantId = 2, Published = true }
                    );
                context.SaveChanges();
            }

            // Use a separate instance of the context to verify correct data was saved to database
            using (var context = new ApplicationDbContext(options))
            {
                var store = new SqlServerSurveyStore(context);
                var result = await store.GetPublishedSurveysAsync();

                Assert.Equal(2, result.Count);
                Assert.True(result.All(x => x.Published));
            }
        }
    }
}
