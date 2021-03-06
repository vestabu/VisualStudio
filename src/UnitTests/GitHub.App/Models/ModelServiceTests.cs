﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Akavache;
using GitHub.Api;
using GitHub.Caches;
using GitHub.Services;
using NSubstitute;
using Octokit;
using Xunit;
using System.Globalization;
using System.Threading;
using GitHub.Models;
using GitHub.Primitives;
using GitHub.Collections;
using ReactiveUI;

public class ModelServiceTests
{
    public class TheGetUserFromCacheMethod : TestBaseClass
    {
        [Fact]
        public async Task RetrievesUserFromCache()
        {
            var apiClient = Substitute.For<IApiClient>();
            var cache = new InMemoryBlobCache();
            await cache.InsertObject<AccountCacheItem>("user", new AccountCacheItem(CreateOctokitUser("octocat")));
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var user = await modelService.GetUserFromCache();

            Assert.Equal("octocat", user.Login);
        }
    }

    public class TheInsertUserMethod : TestBaseClass
    {
        [Fact]
        public async Task AddsUserToCache()
        {
            var apiClient = Substitute.For<IApiClient>();
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var user = await modelService.InsertUser(new AccountCacheItem(CreateOctokitUser("octocat")));

            var cached = await cache.GetObject<AccountCacheItem>("user");
            Assert.Equal("octocat", cached.Login);
        }
    }

    public class TheGetGitIgnoreTemplatesMethod : TestBaseClass
    {
        [Fact]
        public async Task CanRetrieveAndCacheGitIgnores()
        {
            var templates = new[] { "dotnet", "peanuts", "bloomcounty" };
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetGitIgnoreTemplates().Returns(templates.ToObservable());
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var fetched = await modelService.GetGitIgnoreTemplates();

            Assert.Equal(4, fetched.Count);
            Assert.Equal("None", fetched[0].Name);
            Assert.Equal("dotnet", fetched[1].Name);
            Assert.Equal("peanuts", fetched[2].Name);
            Assert.Equal("bloomcounty", fetched[3].Name);
            var cached = await cache.GetObject<IReadOnlyList<string>>("gitignores");
            Assert.Equal(3, cached.Count);
            Assert.Equal("dotnet", cached[0]);
            Assert.Equal("peanuts", cached[1]);
            Assert.Equal("bloomcounty", cached[2]);
        }

        [Fact]
        public async Task ReturnsCollectionOnlyContainingTheNoneOptionnWhenGitIgnoreEndpointNotFound()
        {
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetGitIgnoreTemplates()
                .Returns(Observable.Throw<string>(new NotFoundException("Not Found", HttpStatusCode.NotFound)));
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var fetched = await modelService.GetGitIgnoreTemplates();

            Assert.Equal(1, fetched.Count);
            Assert.Equal("None", fetched[0].Name);
        }

        [Fact]
        public async Task ReturnsCollectionOnlyContainingTheNoneOptionIfCacheReadFails()
        {
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetGitIgnoreTemplates()
                .Returns(Observable.Throw<string>(new NotFoundException("Not Found", HttpStatusCode.NotFound)));
            var cache = Substitute.For<IBlobCache>();
            cache.Get(Args.String)
                .Returns(Observable.Throw<byte[]>(new InvalidOperationException("Unknown")));
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var fetched = await modelService.GetGitIgnoreTemplates();

            Assert.Equal(1, fetched.Count);
            Assert.Equal("None", fetched[0].Name);
        }
    }

    public class TheGetLicensesMethod : TestBaseClass
    {
        [Fact]
        public async Task CanRetrieveAndCacheLicenses()
        {
            var licenses = new[]
            {
                new LicenseMetadata("mit", "MIT", new Uri("https://github.com/")),
                new LicenseMetadata("apache", "Apache", new Uri("https://github.com/"))
            };
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetLicenses().Returns(licenses.ToObservable());
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var fetched = await modelService.GetLicenses();

            Assert.Equal(3, fetched.Count);
            Assert.Equal("None", fetched[0].Name);
            Assert.Equal("MIT", fetched[1].Name);
            Assert.Equal("Apache", fetched[2].Name);
            var cached = await cache.GetObject<IReadOnlyList<ModelService.LicenseCacheItem>>("licenses");
            Assert.Equal(2, cached.Count);
            Assert.Equal("mit", cached[0].Key);
            Assert.Equal("apache", cached[1].Key);
        }

        [Fact]
        public async Task ReturnsCollectionOnlyContainingTheNoneOptionWhenLicenseApiNotFound()
        {
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetLicenses()
                .Returns(Observable.Throw<LicenseMetadata>(new NotFoundException("Not Found", HttpStatusCode.NotFound)));
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var fetched = await modelService.GetLicenses();

            Assert.Equal(1, fetched.Count);
            Assert.Equal("None", fetched[0].Name);
        }

        [Fact]
        public async Task ReturnsCollectionOnlyContainingTheNoneOptionIfCacheReadFails()
        {
            var apiClient = Substitute.For<IApiClient>();
            var cache = Substitute.For<IBlobCache>();
            cache.Get(Args.String)
                .Returns(Observable.Throw<byte[]>(new InvalidOperationException("Unknown")));
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var fetched = await modelService.GetLicenses();

            Assert.Equal(1, fetched.Count);
            Assert.Equal("None", fetched[0].Name);
        }
    }

    public class TheGetAccountsMethod : TestBaseClass
    {
        [Fact]
        public async Task CanRetrieveAndCacheUserAndAccounts()
        {
            var orgs = new[]
            {
                CreateOctokitOrganization("github"),
                CreateOctokitOrganization("fake")
            };
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetUser().Returns(Observable.Return(CreateOctokitUser("snoopy")));
            apiClient.GetOrganizations().Returns(orgs.ToObservable());
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());
            await modelService.InsertUser(new AccountCacheItem { Login = "snoopy" });

            var fetched = await modelService.GetAccounts();

            Assert.Equal(3, fetched.Count);
            Assert.Equal("snoopy", fetched[0].Login);
            Assert.Equal("github", fetched[1].Login);
            Assert.Equal("fake", fetched[2].Login);
            var cachedOrgs = await cache.GetObject<IReadOnlyList<AccountCacheItem>>("snoopy|orgs");
            Assert.Equal(2, cachedOrgs.Count);
            Assert.Equal("github", cachedOrgs[0].Login);
            Assert.Equal("fake", cachedOrgs[1].Login);
            var cachedUser = await cache.GetObject<AccountCacheItem>("user");
            Assert.Equal("snoopy", cachedUser.Login);
        }

        [Fact]
        public async Task CanRetrieveUserFromCacheAndAccountsFromApi()
        {
            var orgs = new[]
            {
                CreateOctokitOrganization("github"),
                CreateOctokitOrganization("fake")
            };
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetOrganizations().Returns(orgs.ToObservable());
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());
            await modelService.InsertUser(new AccountCacheItem(CreateOctokitUser("octocat")));

            var fetched = await modelService.GetAccounts();

            Assert.Equal(3, fetched.Count);
            Assert.Equal("octocat", fetched[0].Login);
            Assert.Equal("github", fetched[1].Login);
            Assert.Equal("fake", fetched[2].Login);
            var cachedOrgs = await cache.GetObject<IReadOnlyList<AccountCacheItem>>("octocat|orgs");
            Assert.Equal(2, cachedOrgs.Count);
            Assert.Equal("github", cachedOrgs[0].Login);
            Assert.Equal("fake", cachedOrgs[1].Login);
            var cachedUser = await cache.GetObject<AccountCacheItem>("user");
            Assert.Equal("octocat", cachedUser.Login);
        }

        [Fact]
        public async Task OnlyRetrievesOneUserEvenIfCacheOrApiReturnsMoreThanOne()
        {
            // This should be impossible, but let's pretend it does happen.
            var users = new[]
            {
                CreateOctokitUser("peppermintpatty"),
                CreateOctokitUser("peppermintpatty")
            };
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetUser().Returns(users.ToObservable());
            apiClient.GetOrganizations().Returns(Observable.Empty<Organization>());
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            var fetched = await modelService.GetAccounts();

            Assert.Equal(1, fetched.Count);
            Assert.Equal("peppermintpatty", fetched[0].Login);
        }
    }

    public class TheGetRepositoriesMethod : TestBaseClass
    {
        [Fact]
        public async Task CanRetrieveAndCacheRepositoriesForUserAndOrganizations()
        {
            var orgs = new[]
            {
                CreateOctokitOrganization("github"),
                CreateOctokitOrganization("octokit")
            };
            var ownedRepos = new[]
            {
                CreateRepository("haacked", "seegit"),
                CreateRepository("haacked", "codehaacks")
            };
            var memberRepos = new[]
            {
                CreateRepository("mojombo", "semver"),
                CreateRepository("ninject", "ninject"),
                CreateRepository("jabbr", "jabbr"),
                CreateRepository("fody", "nullguard")
            };
            var githubRepos = new[]
            {
                CreateRepository("github", "visualstudio")
            };
            var octokitRepos = new[]
            {
                CreateRepository("octokit", "octokit.net"),
                CreateRepository("octokit", "octokit.rb"),
                CreateRepository("octokit", "octokit.objc")
            };
            var apiClient = Substitute.For<IApiClient>();
            apiClient.GetOrganizations().Returns(orgs.ToObservable());
            apiClient.GetUserRepositories(RepositoryType.Owner).Returns(ownedRepos.ToObservable());
            apiClient.GetUserRepositories(RepositoryType.Member).Returns(memberRepos.ToObservable());
            apiClient.GetRepositoriesForOrganization("github").Returns(githubRepos.ToObservable());
            apiClient.GetRepositoriesForOrganization("octokit").Returns(octokitRepos.ToObservable());
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());
            await modelService.InsertUser(new AccountCacheItem { Login = "opus" });

            var fetched = await modelService.GetRepositories().ToList();

            Assert.Equal(4, fetched.Count);
            Assert.Equal(2, fetched[0].Count);
            Assert.Equal(4, fetched[1].Count);
            Assert.Equal(1, fetched[2].Count);
            Assert.Equal(3, fetched[3].Count);
            Assert.Equal("seegit", fetched[0][0].Name);
            Assert.Equal("codehaacks", fetched[0][1].Name);
            Assert.Equal("semver", fetched[1][0].Name);
            Assert.Equal("ninject", fetched[1][1].Name);
            Assert.Equal("jabbr", fetched[1][2].Name);
            Assert.Equal("nullguard", fetched[1][3].Name);
            Assert.Equal("visualstudio", fetched[2][0].Name);
            Assert.Equal("octokit.net", fetched[3][0].Name);
            Assert.Equal("octokit.rb", fetched[3][1].Name);
            Assert.Equal("octokit.objc", fetched[3][2].Name);
            var cachedOwnerRepositories = await cache.GetObject<IReadOnlyList<ModelService.RepositoryCacheItem>>("opus|Owner:repos");
            Assert.Equal(2, cachedOwnerRepositories.Count);
            Assert.Equal("seegit", cachedOwnerRepositories[0].Name);
            Assert.Equal("haacked", cachedOwnerRepositories[0].Owner.Login);
            Assert.Equal("codehaacks", cachedOwnerRepositories[1].Name);
            Assert.Equal("haacked", cachedOwnerRepositories[1].Owner.Login);
            var cachedMemberRepositories = await cache.GetObject<IReadOnlyList<ModelService.RepositoryCacheItem>>("opus|Member:repos");
            Assert.Equal(4, cachedMemberRepositories.Count);
            Assert.Equal("semver", cachedMemberRepositories[0].Name);
            Assert.Equal("mojombo", cachedMemberRepositories[0].Owner.Login);
            Assert.Equal("ninject", cachedMemberRepositories[1].Name);
            Assert.Equal("ninject", cachedMemberRepositories[1].Owner.Login);
            Assert.Equal("jabbr", cachedMemberRepositories[2].Name);
            Assert.Equal("jabbr", cachedMemberRepositories[2].Owner.Login);
            Assert.Equal("nullguard", cachedMemberRepositories[3].Name);
            Assert.Equal("fody", cachedMemberRepositories[3].Owner.Login);
            var cachedGitHubRepositories = await cache.GetObject<IReadOnlyList<ModelService.RepositoryCacheItem>>("opus|github|repos");
            Assert.Equal(1, cachedGitHubRepositories.Count);
            Assert.Equal("seegit", cachedOwnerRepositories[0].Name);
            Assert.Equal("haacked", cachedOwnerRepositories[0].Owner.Login);
            Assert.Equal("codehaacks", cachedOwnerRepositories[1].Name);
            Assert.Equal("haacked", cachedOwnerRepositories[1].Owner.Login);
            var cachedOctokitRepositories = await cache.GetObject<IReadOnlyList<ModelService.RepositoryCacheItem>>("opus|octokit|repos");
            Assert.Equal("octokit.net", cachedOctokitRepositories[0].Name);
            Assert.Equal("octokit", cachedOctokitRepositories[0].Owner.Login);
            Assert.Equal("octokit.rb", cachedOctokitRepositories[1].Name);
            Assert.Equal("octokit", cachedOctokitRepositories[1].Owner.Login);
            Assert.Equal("octokit.objc", cachedOctokitRepositories[2].Name);
            Assert.Equal("octokit", cachedOctokitRepositories[2].Owner.Login);
        }

        [Fact]
        public async Task WhenNotLoggedInReturnsEmptyCollection()
        {
            var apiClient = Substitute.For<IApiClient>();
            var modelService = new ModelService(apiClient, new InMemoryBlobCache(), Substitute.For<IAvatarProvider>());

            var repos = await modelService.GetRepositories();

            Assert.Equal(0, repos.Count);
        }

        [Fact]
        public async Task WhenLoggedInDoesNotBlowUpOnUnexpectedNetworkProblems()
        {
            var apiClient = Substitute.For<IApiClient>();
            var modelService = new ModelService(apiClient, new InMemoryBlobCache(), Substitute.For<IAvatarProvider>());
            apiClient.GetOrganizations()
                .Returns(Observable.Throw<Organization>(new NotFoundException("Not Found", HttpStatusCode.NotFound)));

            var repos = await modelService.GetRepositories();

            Assert.Equal(0, repos.Count);
        }
    }

    public class TheInvalidateAllMethod : TestBaseClass
    {
        [Fact]
        public async Task InvalidatesTheCache()
        {
            var apiClient = Substitute.For<IApiClient>();
            var cache = new InMemoryBlobCache();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());
            var user = await modelService.InsertUser(new AccountCacheItem(CreateOctokitUser("octocat")));
            Assert.Equal(1, (await cache.GetAllObjects<AccountCacheItem>()).Count());

            await modelService.InvalidateAll();

            Assert.Equal(0, (await cache.GetAllObjects<AccountCacheItem>()).Count());
        }

        [Fact]
        public async Task VaccumsTheCache()
        {
            var apiClient = Substitute.For<IApiClient>();
            var cache = Substitute.For<IBlobCache>();
            cache.InvalidateAll().Returns(Observable.Return(Unit.Default));
            var received = false;
            cache.Vacuum().Returns(x =>
            {
                received = true;
                return Observable.Return(Unit.Default);
            });
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());

            await modelService.InvalidateAll();
            Assert.True(received);
        }
    }

    public class TheGetPullRequestsMethod : TestBaseClass
    {
        [Fact]
        public async Task NonExpiredIndexReturnsCache()
        {
            var expected = 5;

            var username = "octocat";
            var reponame = "repo";

            var cache = new InMemoryBlobCache();
            var apiClient = Substitute.For<IApiClient>();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());
            var user = CreateOctokitUser(username);
            apiClient.GetUser().Returns(Observable.Return(user));
            apiClient.GetOrganizations().Returns(Observable.Empty<Organization>());
            var act = modelService.GetAccounts().ToEnumerable().First().First();

            var repo = Substitute.For<ISimpleRepositoryModel>();
            repo.Name.Returns(reponame);
            repo.CloneUrl.Returns(new UriString("https://github.com/" + username + "/" + reponame));

            var indexKey = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|pr", user.Login, repo.Name);

            var prcache = Enumerable.Range(1, expected)
                .Select(id => CreatePullRequest(user, id, ItemState.Open, "Cache " + id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0));

            // seed the cache
            prcache
                .Select(item => new ModelService.PullRequestCacheItem(item))
                .Select(item => item.Save<ModelService.PullRequestCacheItem>(cache, indexKey).ToEnumerable().First())
                .SelectMany(item => CacheIndex.AddAndSaveToIndex(cache, indexKey, item).ToEnumerable())
                .ToList();

            var prlive = Observable.Range(1, expected)
                .Select(id => CreatePullRequest(user, id, ItemState.Open, "Live " + id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0))
                .DelaySubscription(TimeSpan.FromMilliseconds(10));

            apiClient.GetPullRequestsForRepository(user.Login, repo.Name).Returns(prlive);

            await modelService.InsertUser(new AccountCacheItem(user));
            var col = modelService.GetPullRequests(repo);
            col.ProcessingDelay = TimeSpan.Zero;

            var count = 0;
            var evt = new ManualResetEvent(false);
            col.Subscribe(t =>
            {
                if (++count == expected)
                    evt.Set();
            }, () => { });


            evt.WaitOne();
            evt.Reset();

            Assert.Collection(col, col.Select(x => new Action<IPullRequestModel>(t => Assert.True(x.Title.StartsWith("Cache")))).ToArray());
        }

        [Fact]
        public async Task ExpiredIndexReturnsLive()
        {
            var expected = 5;

            var username = "octocat";
            var reponame = "repo";

            var cache = new InMemoryBlobCache();
            var apiClient = Substitute.For<IApiClient>();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());
            var user = CreateOctokitUser(username);
            apiClient.GetUser().Returns(Observable.Return(user));
            apiClient.GetOrganizations().Returns(Observable.Empty<Organization>());
            var act = modelService.GetAccounts().ToEnumerable().First().First();

            var repo = Substitute.For<ISimpleRepositoryModel>();
            repo.Name.Returns(reponame);
            repo.CloneUrl.Returns(new UriString("https://github.com/" + username + "/" + reponame));

            var indexKey = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|pr", user.Login, repo.Name);

            var prcache = Enumerable.Range(1, expected)
                .Select(id => CreatePullRequest(user, id, ItemState.Open, "Cache " + id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0));

            // seed the cache
            prcache
                .Select(item => new ModelService.PullRequestCacheItem(item))
                .Select(item => item.Save<ModelService.PullRequestCacheItem>(cache, indexKey).ToEnumerable().First())
                .SelectMany(item => CacheIndex.AddAndSaveToIndex(cache, indexKey, item).ToEnumerable())
                .ToList();

            // expire the index
            var indexobj = await cache.GetObject<CacheIndex>(indexKey);
            indexobj.UpdatedAt = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(6);
            await cache.InsertObject(indexKey, indexobj);

            var prlive = Observable.Range(1, expected)
                .Select(id => CreatePullRequest(user, id, ItemState.Open, "Live " + id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0))
                .DelaySubscription(TimeSpan.FromMilliseconds(10));

            apiClient.GetPullRequestsForRepository(user.Login, repo.Name).Returns(prlive);

            await modelService.InsertUser(new AccountCacheItem(user));
            var col = modelService.GetPullRequests(repo);
            col.ProcessingDelay = TimeSpan.Zero;

            var count = 0;
            var evt = new ManualResetEvent(false);
            col.Subscribe(t =>
            {
                if (++count == expected * 2)
                    evt.Set();
            }, () => { });

            
            evt.WaitOne();
            evt.Reset();

            Assert.Collection(col, col.Select(x => new Action<IPullRequestModel>(t => Assert.True(x.Title.StartsWith("Live")))).ToArray());
        }

        [Fact]
        public async Task ExpiredIndexClearsItems()
        {
            var expected = 5;

            var username = "octocat";
            var reponame = "repo";

            var cache = new InMemoryBlobCache();
            var apiClient = Substitute.For<IApiClient>();
            var modelService = new ModelService(apiClient, cache, Substitute.For<IAvatarProvider>());
            var user = CreateOctokitUser(username);
            apiClient.GetUser().Returns(Observable.Return(user));
            apiClient.GetOrganizations().Returns(Observable.Empty<Organization>());
            var act = modelService.GetAccounts().ToEnumerable().First().First();

            var repo = Substitute.For<ISimpleRepositoryModel>();
            repo.Name.Returns(reponame);
            repo.CloneUrl.Returns(new UriString("https://github.com/" + username + "/" + reponame));

            var indexKey = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|pr", user.Login, repo.Name);

            var prcache = Enumerable.Range(1, expected)
                .Select(id => CreatePullRequest(user, id, ItemState.Open, "Cache " + id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0));

            // seed the cache
            prcache
                .Select(item => new ModelService.PullRequestCacheItem(item))
                .Select(item => item.Save<ModelService.PullRequestCacheItem>(cache, indexKey).ToEnumerable().First())
                .SelectMany(item => CacheIndex.AddAndSaveToIndex(cache, indexKey, item).ToEnumerable())
                .ToList();

            // expire the index
            var indexobj = await cache.GetObject<CacheIndex>(indexKey);
            indexobj.UpdatedAt = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(6);
            await cache.InsertObject(indexKey, indexobj);

            var prlive = Observable.Range(5, expected)
                .Select(id => CreatePullRequest(user, id, ItemState.Open, "Live " + id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0))
                .DelaySubscription(TimeSpan.FromMilliseconds(10));

            apiClient.GetPullRequestsForRepository(user.Login, repo.Name).Returns(prlive);

            await modelService.InsertUser(new AccountCacheItem(user));
            var col = modelService.GetPullRequests(repo);
            col.ProcessingDelay = TimeSpan.Zero;

            var count = 0;
            var evt = new ManualResetEvent(false);
            col.Subscribe(t =>
            {
                // we get all the items from the cache (items 1-5), all the items from the live (items 5-9),
                // and 4 deletions (items 1-4) because the cache expired the items that were not
                // a part of the live data
                if (++count == 14)
                    evt.Set();
            }, () => { });


            evt.WaitOne();
            evt.Reset();

            Assert.Equal(5, col.Count);
            Assert.Collection(col, 
                t => { Assert.True(t.Title.StartsWith("Live")); Assert.Equal(5, t.Number); },
                t => { Assert.True(t.Title.StartsWith("Live")); Assert.Equal(6, t.Number); },
                t => { Assert.True(t.Title.StartsWith("Live")); Assert.Equal(7, t.Number); },
                t => { Assert.True(t.Title.StartsWith("Live")); Assert.Equal(8, t.Number); },
                t => { Assert.True(t.Title.StartsWith("Live")); Assert.Equal(9, t.Number); }
            );
        }
    }

    static User CreateOctokitUser(string login)
    {
        return new User("https://url", "", "", 1, "GitHub", DateTimeOffset.UtcNow, 0, "email", 100, 100, true, "http://url", 10, 42, "somewhere", login, "Who cares", 1, new Plan(), 1, 1, 1, "https://url", false);
    }

    static Organization CreateOctokitOrganization(string login)
    {
        return new Organization("https://url", "", "", 1, "GitHub", DateTimeOffset.UtcNow, 0, "email", 100, 100, true, "http://url", 10, 42, "somewhere", login, "Who cares", 1, new Plan(), 1, 1, 1, "https://url", "billing");
    }

    static Repository CreateRepository(string owner, string name)
    {
        return new Repository("https://url", "https://url", "https://url", "https://url", "https://url", "https://url", "https://url", 1, CreateOctokitUser(owner), name, "fullname", "description", "https://url", "c#", false, false, 0, 0, 0, "master", 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new RepositoryPermissions(), null, null, null, true, false, false);
    }

    static PullRequest CreatePullRequest(User user, int id, ItemState state, string title,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, int commentCount)
    {
        var uri = new Uri("https://url");
        return new PullRequest(uri, uri, uri, uri, uri, uri,
            id, state, title, "", createdAt, updatedAt,
            null, null, null, null, user, null, false, null,
            commentCount, 0, 0, 0, 0,
            null, false);
    }
}
