using System;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Dalamud.Extensions.MicrosoftLogging;

public sealed class DalamudLoggerProvider : ILoggerProvider, IDisposable, ISupportExternalScope
{
	private readonly IPluginLog _pluginLog;

	private readonly Func<string, string?> _categoryNameFormatter = (string t) => t;

	private IExternalScopeProvider? _scopeProvider;

	public DalamudLoggerProvider(IPluginLog pluginLog, Func<string, string?>? categoryNameFormatter = null)
	{
		_pluginLog = pluginLog;
		if (categoryNameFormatter != null)
		{
			_categoryNameFormatter = categoryNameFormatter;
		}
	}

	ILogger ILoggerProvider.CreateLogger(string categoryName)
	{
		return CreateLoggerImpl(categoryName);
	}

	public ILogger CreateLogger(Type type)
	{
		return CreateLoggerImpl(type.FullName ?? type.ToString());
	}

	public ILogger CreateLogger<T>()
	{
		return CreateLoggerImpl(typeof(T).FullName ?? typeof(T).ToString());
	}

	private ILogger CreateLoggerImpl(string categoryName)
	{
		return new DalamudLogger(_categoryNameFormatter(categoryName), _scopeProvider, _pluginLog);
	}

	public void SetScopeProvider(IExternalScopeProvider scopeProvider)
	{
		_scopeProvider = scopeProvider;
	}

	public void Dispose()
	{
	}
}
