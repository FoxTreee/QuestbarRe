using Godot;
using System;
using System.Collections.Generic;

public enum DebugLogCategory
{
	General,
	Threat,
	Damage,
	Ability,
	Encounter,
	Error
}

public static class DebugLog
{
	private readonly record struct LogEntry(
		DateTime Timestamp,
		DebugLogCategory Category,
		string Message);

	private static readonly List<LogEntry> BufferedEntries = new();
	private static Action<DateTime, DebugLogCategory, string>? _messageLogged;

	public static void Print(
		object? message,
		DebugLogCategory? category = null)
	{
		string text =
			message?.ToString() ?? "Null";

		DateTime timestamp =
			DateTime.Now;

		DebugLogCategory resolvedCategory =
			category ?? Classify(text);

		GD.Print(text);

		if (_messageLogged is null)
		{
			BufferedEntries.Add(
				new LogEntry(
					timestamp,
					resolvedCategory,
					text));
			return;
		}

		_messageLogged.Invoke(
			timestamp,
			resolvedCategory,
			text);
	}

	public static void Subscribe(
		Action<DateTime, DebugLogCategory, string> listener)
	{
		_messageLogged += listener;

		foreach (LogEntry entry in BufferedEntries)
		{
			listener(
				entry.Timestamp,
				entry.Category,
				entry.Message);
		}

		BufferedEntries.Clear();
	}

	public static void Unsubscribe(
		Action<DateTime, DebugLogCategory, string> listener)
	{
		_messageLogged -= listener;
	}

	private static DebugLogCategory Classify(string message)
	{
		if (ContainsAny(
			message,
			"error",
			"warning",
			"failed",
			"could not",
			"missing",
			"invalid",
			"rejected"))
		{
			return DebugLogCategory.Error;
		}

		if (ContainsAny(
			message,
			"threat",
			"aggro"))
		{
			return DebugLogCategory.Threat;
		}

		if (ContainsAny(
			message,
			"ability",
			"taunt",
			"cooldown",
			"forced target"))
		{
			return DebugLogCategory.Ability;
		}

		if (ContainsAny(
			message,
			"damage",
			"attack",
			"impact",
			"projectile",
			"incapacitated",
			"dealt"))
		{
			return DebugLogCategory.Damage;
		}

		if (ContainsAny(
			message,
			"encounter",
			"monster spawn",
			"spawned",
			"active monsters",
			"died",
			"death",
			"lethal",
			"victory",
			"defeat",
			"journey"))
		{
			return DebugLogCategory.Encounter;
		}

		return DebugLogCategory.General;
	}

	private static bool ContainsAny(
		string message,
		params string[] terms)
	{
		foreach (string term in terms)
		{
			if (message.Contains(
				term,
				StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
