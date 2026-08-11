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

	/// <summary>
	/// Performs the print operation for Debug Log.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the subscribe operation for Debug Log.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the unsubscribe operation for Debug Log.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public static void Unsubscribe(
		Action<DateTime, DebugLogCategory, string> listener)
	{
		_messageLogged -= listener;
	}

	/// <summary>
	/// Performs the classify operation for Debug Log.
	/// Uses the supplied arguments and current state and returns the resulting debug log category to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the contains any operation for Debug Log.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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
