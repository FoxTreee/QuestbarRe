using Godot;
using System;
using System.Collections.Generic;

public static class DebugLog
{
	private readonly record struct LogEntry(
		DateTime Timestamp,
		string Message);

	private static readonly List<LogEntry> BufferedEntries = new();
	private static Action<DateTime, string>? _messageLogged;

	public static void Print(object? message)
	{
		string text =
			message?.ToString() ?? "Null";

		DateTime timestamp =
			DateTime.Now;

		GD.Print(text);

		if (_messageLogged is null)
		{
			BufferedEntries.Add(
				new LogEntry(timestamp, text));
			return;
		}

		_messageLogged.Invoke(
			timestamp,
			text);
	}

	public static void Subscribe(
	Action<DateTime, string> listener)
	{
		_messageLogged += listener;

		foreach (LogEntry entry in BufferedEntries)
		{
			listener(
				entry.Timestamp,
				entry.Message);
		}

		BufferedEntries.Clear();
	}

	public static void Unsubscribe(
	Action<DateTime, string> listener)
	{
		_messageLogged -= listener;
	}
}
