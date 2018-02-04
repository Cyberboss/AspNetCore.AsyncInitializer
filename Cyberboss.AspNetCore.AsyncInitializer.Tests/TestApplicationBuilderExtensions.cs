using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cyberboss.AspNetCore.AsyncInitializer.Tests
{
	[TestClass]
	public sealed class TestApplicationBuilderExtensions
	{
		[TestMethod]
		public void TestBadGenericInvoke()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			Assert.ThrowsException<ArgumentNullException>(() => mockAppBuilder.Object.UseAsyncInitialization<TestApplicationBuilderExtensions>(null));
		}

		[TestMethod]
		public async Task TestGenericAsyncInitializerIsRun()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			var mockAppLifetime = new Mock<IApplicationLifetime>();
			var mockServiceProvider = new Mock<IServiceProvider>();

			var startCancellationTokenSource = new CancellationTokenSource();
			var stopCancellationTokenSource = new CancellationTokenSource();

			mockAppLifetime.SetupGet(x => x.ApplicationStarted).Returns(startCancellationTokenSource.Token);
			mockAppLifetime.SetupGet(x => x.ApplicationStopping).Returns(stopCancellationTokenSource.Token);
			mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationLifetime))).Returns(mockAppLifetime.Object);
			mockServiceProvider.Setup(x => x.GetService(typeof(TestApplicationBuilderExtensions))).Returns(this);
			mockAppBuilder.SetupGet(x => x.ApplicationServices).Returns(mockServiceProvider.Object);

			Func<RequestDelegate, RequestDelegate> function = null;
			mockAppBuilder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Callback<Func<RequestDelegate, RequestDelegate>>(passed => function = passed);

			var runCount = 0;

			mockAppBuilder.Object.UseAsyncInitialization<TestApplicationBuilderExtensions>((dependency, cancellationToken) =>
			{
				Assert.AreEqual(stopCancellationTokenSource.Token, cancellationToken);
				Assert.AreSame(this, dependency);
				++runCount;
				return Task.CompletedTask;
			});

			Assert.AreEqual(0, runCount);
			startCancellationTokenSource.Cancel();
			Assert.AreEqual(1, runCount);

			await function(new RequestDelegate(_ => Task.CompletedTask))(null).ConfigureAwait(false);
		}

		[TestMethod]
		public void TestGenericFailsAtCallTimeWithNoDependencyResolved()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			var mockAppLifetime = new Mock<IApplicationLifetime>();
			var mockServiceProvider = new Mock<IServiceProvider>();

			var startCancellationTokenSource = new CancellationTokenSource();
			var stopCancellationTokenSource = new CancellationTokenSource();

			mockAppLifetime.SetupGet(x => x.ApplicationStarted).Returns(startCancellationTokenSource.Token);
			mockAppLifetime.SetupGet(x => x.ApplicationStopping).Returns(stopCancellationTokenSource.Token);
			mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationLifetime))).Returns(mockAppLifetime.Object);
			mockAppBuilder.SetupGet(x => x.ApplicationServices).Returns(mockServiceProvider.Object);

			Assert.ThrowsException<InvalidOperationException>(() => mockAppBuilder.Object.UseAsyncInitialization<TestApplicationBuilderExtensions>((dependency, cancellationToken) => Task.CompletedTask));
		}

		[TestMethod]
		public async Task TestGenericAsyncInitializerIsAwaitedOnRequest()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			var mockAppLifetime = new Mock<IApplicationLifetime>();
			var mockServiceProvider = new Mock<IServiceProvider>();

			var startCancellationTokenSource = new CancellationTokenSource();
			var stopCancellationTokenSource = new CancellationTokenSource();

			mockAppLifetime.SetupGet(x => x.ApplicationStarted).Returns(startCancellationTokenSource.Token);
			mockAppLifetime.SetupGet(x => x.ApplicationStopping).Returns(stopCancellationTokenSource.Token);
			mockServiceProvider.Setup(x => x.GetService(typeof(TestApplicationBuilderExtensions))).Returns(this);
			mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationLifetime))).Returns(mockAppLifetime.Object);
			mockAppBuilder.SetupGet(x => x.ApplicationServices).Returns(mockServiceProvider.Object);

			Func<RequestDelegate, RequestDelegate> function = null;
			mockAppBuilder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Callback<Func<RequestDelegate, RequestDelegate>>(passed => function = passed);

			var originalException = new InvalidOperationException();
			//async is important to prevent the assignment from throwing the exception
			mockAppBuilder.Object.UseAsyncInitialization<TestApplicationBuilderExtensions>(async (dependency, cancellationToken) =>
			{
				Assert.AreEqual(stopCancellationTokenSource.Token, cancellationToken);
				Assert.AreSame(this, dependency);
				await Task.CompletedTask.ConfigureAwait(false);
				throw originalException;
			});

			startCancellationTokenSource.Cancel();

			var taskDelegate = function(new RequestDelegate(_ => Task.CompletedTask));

			var thrownException = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => taskDelegate(null)).ConfigureAwait(false);

			Assert.AreSame(originalException, thrownException);
		}

		[TestMethod]
		public async Task TestGenericCancellationIsAwaitedOnRequest()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			var mockAppLifetime = new Mock<IApplicationLifetime>();
			var mockServiceProvider = new Mock<IServiceProvider>();

			var startCancellationTokenSource = new CancellationTokenSource();
			var stopCancellationTokenSource = new CancellationTokenSource();

			mockAppLifetime.SetupGet(x => x.ApplicationStarted).Returns(startCancellationTokenSource.Token);
			mockAppLifetime.SetupGet(x => x.ApplicationStopping).Returns(stopCancellationTokenSource.Token);
			mockServiceProvider.Setup(x => x.GetService(typeof(TestApplicationBuilderExtensions))).Returns(this);
			mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationLifetime))).Returns(mockAppLifetime.Object);
			mockAppBuilder.SetupGet(x => x.ApplicationServices).Returns(mockServiceProvider.Object);

			Func<RequestDelegate, RequestDelegate> function = null;
			mockAppBuilder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Callback<Func<RequestDelegate, RequestDelegate>>(passed => function = passed);

			//async is important to prevent the assignment from throwing the exception
			mockAppBuilder.Object.UseAsyncInitialization<TestApplicationBuilderExtensions>(async (dependency, cancellationToken) =>
			{
				Assert.AreEqual(stopCancellationTokenSource.Token, cancellationToken);
				Assert.AreSame(this, dependency);
				cancellationToken.ThrowIfCancellationRequested();
				await Task.CompletedTask.ConfigureAwait(false);
			});

			stopCancellationTokenSource.Cancel();
			startCancellationTokenSource.Cancel();

			var taskDelegate = function(new RequestDelegate(_ => Task.CompletedTask));

			await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => taskDelegate(null)).ConfigureAwait(false);
		}

		[TestMethod]
		public void TestBadInvoke()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			Assert.ThrowsException<ArgumentNullException>(() => mockAppBuilder.Object.UseAsyncInitialization(null));
		}

		[TestMethod]
		public async Task TestAsyncInitializerIsRun()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			var mockAppLifetime = new Mock<IApplicationLifetime>();
			var mockServiceProvider = new Mock<IServiceProvider>();

			var startCancellationTokenSource = new CancellationTokenSource();
			var stopCancellationTokenSource = new CancellationTokenSource();

			mockAppLifetime.SetupGet(x => x.ApplicationStarted).Returns(startCancellationTokenSource.Token);
			mockAppLifetime.SetupGet(x => x.ApplicationStopping).Returns(stopCancellationTokenSource.Token);
			mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationLifetime))).Returns(mockAppLifetime.Object);
			mockAppBuilder.SetupGet(x => x.ApplicationServices).Returns(mockServiceProvider.Object);

			Func<RequestDelegate, RequestDelegate> function = null;
			mockAppBuilder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Callback<Func<RequestDelegate, RequestDelegate>>(passed => function = passed);

			var runCount = 0;

			mockAppBuilder.Object.UseAsyncInitialization((cancellationToken) =>
			{
				Assert.AreEqual(stopCancellationTokenSource.Token, cancellationToken);
				++runCount;
				return Task.CompletedTask;
			});

			Assert.AreEqual(0, runCount);
			startCancellationTokenSource.Cancel();
			Assert.AreEqual(1, runCount);

			await function(new RequestDelegate(_ => Task.CompletedTask))(null).ConfigureAwait(false);
		}

		[TestMethod]
		public async Task TestAsyncInitializerIsAwaitedOnRequest()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			var mockAppLifetime = new Mock<IApplicationLifetime>();
			var mockServiceProvider = new Mock<IServiceProvider>();

			var startCancellationTokenSource = new CancellationTokenSource();
			var stopCancellationTokenSource = new CancellationTokenSource();

			mockAppLifetime.SetupGet(x => x.ApplicationStarted).Returns(startCancellationTokenSource.Token);
			mockAppLifetime.SetupGet(x => x.ApplicationStopping).Returns(stopCancellationTokenSource.Token);
			mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationLifetime))).Returns(mockAppLifetime.Object);
			mockAppBuilder.SetupGet(x => x.ApplicationServices).Returns(mockServiceProvider.Object);

			Func<RequestDelegate, RequestDelegate> function = null;
			mockAppBuilder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Callback<Func<RequestDelegate, RequestDelegate>>(passed => function = passed);

			var originalException = new InvalidOperationException();
			//async is important to prevent the assignment from throwing the exception
			mockAppBuilder.Object.UseAsyncInitialization(async (cancellationToken) =>
			{
				await Task.CompletedTask.ConfigureAwait(false);
				throw originalException;
			});

			startCancellationTokenSource.Cancel();

			var taskDelegate = function(new RequestDelegate(_ => Task.CompletedTask));

			var thrownException = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => taskDelegate(null)).ConfigureAwait(false);

			Assert.AreSame(originalException, thrownException);
		}

		[TestMethod]
		public async Task TestCancellationIsAwaitedOnRequest()
		{
			var mockAppBuilder = new Mock<IApplicationBuilder>();
			var mockAppLifetime = new Mock<IApplicationLifetime>();
			var mockServiceProvider = new Mock<IServiceProvider>();

			var startCancellationTokenSource = new CancellationTokenSource();
			var stopCancellationTokenSource = new CancellationTokenSource();

			mockAppLifetime.SetupGet(x => x.ApplicationStarted).Returns(startCancellationTokenSource.Token);
			mockAppLifetime.SetupGet(x => x.ApplicationStopping).Returns(stopCancellationTokenSource.Token);
			mockServiceProvider.Setup(x => x.GetService(typeof(IApplicationLifetime))).Returns(mockAppLifetime.Object);
			mockAppBuilder.SetupGet(x => x.ApplicationServices).Returns(mockServiceProvider.Object);

			Func<RequestDelegate, RequestDelegate> function = null;
			mockAppBuilder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Callback<Func<RequestDelegate, RequestDelegate>>(passed => function = passed);
			
			//async is important to prevent the assignment from throwing the exception
			mockAppBuilder.Object.UseAsyncInitialization(async (cancellationToken) =>
			{
				Assert.AreEqual(stopCancellationTokenSource.Token, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				await Task.CompletedTask.ConfigureAwait(false);
			});

			stopCancellationTokenSource.Cancel();
			startCancellationTokenSource.Cancel();

			var taskDelegate = function(new RequestDelegate(_ => Task.CompletedTask));

			await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => taskDelegate(null)).ConfigureAwait(false);
		}
	}
}
