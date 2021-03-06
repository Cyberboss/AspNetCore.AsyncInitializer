﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Cyberboss.AspNetCore.AsyncInitializer
{
	/// <summary>
	/// Asynchronous initialization extensions for <see cref="IApplicationBuilder"/>
	/// </summary>
	public static class ApplicationBuilderExtensions
	{
		/// <summary>
		/// Runs a <see cref="Task"/> once <paramref name="applicationBuilder"/> has started
		/// </summary>
		/// <typeparam name="TDependency">A dependency available in <see cref="IApplicationBuilder.ApplicationServices"/></typeparam>
		/// <param name="applicationBuilder">The <see cref="IApplicationBuilder"/> to <see cref="IApplicationBuilder.Use(Func{Microsoft.AspNetCore.Http.RequestDelegate, Microsoft.AspNetCore.Http.RequestDelegate})"/></param>
		/// <param name="asyncInitializer">A <see cref="Func{T1, T2, TResult}"/> taking a <typeparamref name="TDependency"/> and <see cref="CancellationToken"/> to run at startup. Note that synchronously thrown exceptions will be unhandled</param>
		/// <exception cref="ArgumentNullException"><paramref name="applicationBuilder"/> or <paramref name="asyncInitializer"/> is <see langword="null"/></exception>
		/// <exception cref="InvalidOperationException">There is no service of type <typeparamref name="TDependency"/></exception>
		public static void UseAsyncInitialization<TDependency>(this IApplicationBuilder applicationBuilder, Func<TDependency, CancellationToken, Task> asyncInitializer)
		{
			if (applicationBuilder == null)
				throw new ArgumentNullException(nameof(applicationBuilder));
			if (asyncInitializer == null)
				throw new ArgumentNullException(nameof(asyncInitializer));
			
			applicationBuilder.UseAsyncInitialization((cancellationToken) => asyncInitializer(applicationBuilder.ApplicationServices.GetRequiredService<TDependency>(), cancellationToken));
		}

		/// <summary>
		/// Runs a <see cref="Task"/> once <paramref name="applicationBuilder"/> has started
		/// </summary>
		/// <param name="applicationBuilder">The <see cref="IApplicationBuilder"/> to <see cref="IApplicationBuilder.Use(Func{Microsoft.AspNetCore.Http.RequestDelegate, Microsoft.AspNetCore.Http.RequestDelegate})"/></param>
		/// <param name="asyncInitializer">A <see cref="Func{T, TResult}"/> taking a <see cref="CancellationToken"/> to run at startup. Note that synchronously thrown exceptions will be unhandled</param>
		/// <exception cref="ArgumentNullException"><paramref name="applicationBuilder"/> or <paramref name="asyncInitializer"/> is <see langword="null"/></exception>
		public static void UseAsyncInitialization(this IApplicationBuilder applicationBuilder, Func<CancellationToken, Task> asyncInitializer)
		{
			if (applicationBuilder == null)
				throw new ArgumentNullException(nameof(applicationBuilder));
			if (asyncInitializer == null)
				throw new ArgumentNullException(nameof(asyncInitializer));

			var applicationLifetime = applicationBuilder.ApplicationServices.GetRequiredService<IApplicationLifetime>();
			Task initializationTask = null;
			var tcs = new TaskCompletionSource<object>();

			async Task AsyncProcess()
			{
				using (applicationBuilder.ApplicationServices.CreateScope())
					await asyncInitializer(applicationLifetime.ApplicationStopping).ConfigureAwait(false);
			}

			applicationLifetime.ApplicationStarted.Register(() =>
			{
				initializationTask = AsyncProcess();
				tcs.SetResult(null);
			});

			var applicationStopping = applicationLifetime.ApplicationStopping;
			applicationBuilder.Use(async (context, next) =>
			{
				await tcs.Task.ConfigureAwait(false);
				if(!initializationTask.IsCompleted || applicationStopping.IsCancellationRequested)
				{
					context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
					return;
				}
				await initializationTask.ConfigureAwait(false);
				await next().ConfigureAwait(false);
			});
		}
	}
}
