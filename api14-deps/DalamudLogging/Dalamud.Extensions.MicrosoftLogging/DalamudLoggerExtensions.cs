using System;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dalamud.Extensions.MicrosoftLogging;

public static class DalamudLoggerExtensions
{
	public static ILoggingBuilder AddDalamudLogger(this ILoggingBuilder builder, IPluginLog pluginLog, Func<string, string?>? categoryNameFormatter = null)
	{
		builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton((ILoggerProvider)new DalamudLoggerProvider(pluginLog, categoryNameFormatter)));
		return builder;
	}
}
