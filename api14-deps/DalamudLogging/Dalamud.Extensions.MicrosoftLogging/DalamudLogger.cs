using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Dalamud.Extensions.MicrosoftLogging;

public sealed class DalamudLogger : ILogger
{
	private sealed class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new NullScope();

		private NullScope()
		{
		}

		public void Dispose()
		{
		}
	}

	private readonly string? _name;

	private readonly IExternalScopeProvider? _scopeProvider;

	private readonly IPluginLog _pluginLogDelegate;

	public DalamudLogger(string? name, IExternalScopeProvider? scopeProvider, IPluginLog pluginLogDelegate)
	{
		_name = name;
		_scopeProvider = scopeProvider;
		_pluginLogDelegate = pluginLogDelegate;
	}

	public IDisposable BeginScope<TState>(TState state) where TState : notnull
	{
		return _scopeProvider?.Push(state) ?? NullScope.Instance;
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		if (logLevel != LogLevel.None)
		{
			return IsEnabled(ToSerilogLevel(logLevel));
		}
		return false;
	}

	private bool IsEnabled(LogEventLevel logEventLevel)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return logEventLevel >= _pluginLogDelegate.MinimumLogLevel;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		if (logLevel == LogLevel.None)
		{
			return;
		}
		LogEventLevel val = ToSerilogLevel(logLevel);
		if (!IsEnabled(val))
		{
			return;
		}
		if (formatter == null)
		{
			throw new ArgumentNullException("formatter");
		}
		StringBuilder stringBuilder = new StringBuilder();
		_scopeProvider?.ForEachScope(delegate(object? scope, StringBuilder builder)
		{
			if (scope is IEnumerable<KeyValuePair<string, object>> enumerable)
			{
				{
					foreach (KeyValuePair<string, object> item in enumerable)
					{
						builder.Append('<').Append(item.Key).Append('=')
							.Append(item.Value)
							.Append("> ");
					}
					return;
				}
			}
			if (scope != null)
			{
				builder.Append('<').Append(scope).Append("> ");
			}
		}, stringBuilder);
		if (!string.IsNullOrEmpty(_name))
		{
			stringBuilder.Append(_name).Append(": ");
		}
		stringBuilder.Append(formatter(state, null));
		_pluginLogDelegate.Write(val, exception, stringBuilder.ToString(), Array.Empty<object>());
	}

	private LogEventLevel ToSerilogLevel(LogLevel logLevel)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		return (LogEventLevel)(logLevel switch
		{
			LogLevel.Critical => 5,
			LogLevel.Error => 4,
			LogLevel.Warning => 3,
			LogLevel.Information => 2,
			LogLevel.Debug => 1,
			LogLevel.Trace => 0,
			_ => throw new ArgumentOutOfRangeException("logLevel", logLevel, null),
		});
	}
}
