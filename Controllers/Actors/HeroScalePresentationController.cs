using System.Collections.Generic;
using Godot;

public partial class HeroScalePresentationController : Node
{
	[ExportCategory("Dependencies")]
	[Export]
	public Node ActorLayer { get; set; } = null!;

	[Export]
	public DesktopWindowHostController WindowHost
	{
		get;
		set;
	} = null!;

	private readonly Dictionary<HeroActorController, Vector2>
		_expandedVisualScales = new();

	public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		ActorLayer.ChildEnteredTree +=
			OnActorEnteredTree;

		ActorLayer.ChildExitingTree +=
			OnActorExitingTree;

		WindowHost.ExpandedChanged +=
			OnExpandedChanged;

		WindowHost.PlacementSettings.Changed +=
			OnPlacementSettingsChanged;

		RegisterExistingHeroes();

		Callable.From(ApplyCurrentScale)
			.CallDeferred();
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(ActorLayer))
		{
			ActorLayer.ChildEnteredTree -=
				OnActorEnteredTree;

			ActorLayer.ChildExitingTree -=
				OnActorExitingTree;
		}

		if (GodotObject.IsInstanceValid(WindowHost))
		{
			WindowHost.ExpandedChanged -=
				OnExpandedChanged;

			if (WindowHost.PlacementSettings is not null)
			{
				WindowHost.PlacementSettings.Changed -=
					OnPlacementSettingsChanged;
			}
		}

		_expandedVisualScales.Clear();
	}

	private void RegisterExistingHeroes()
	{
		foreach (Node actor in ActorLayer.GetChildren())
			RegisterHero(actor as HeroActorController);
	}

	private void OnActorEnteredTree(Node actor)
	{
		RegisterHero(actor as HeroActorController);
		ApplyCurrentScale();
	}

	private void OnActorExitingTree(Node actor)
	{
		if (actor is HeroActorController hero)
			_expandedVisualScales.Remove(hero);
	}

	private void RegisterHero(HeroActorController? hero)
	{
		if (hero is null
			|| !GodotObject.IsInstanceValid(hero.VisualRoot)
			|| _expandedVisualScales.ContainsKey(hero))
		{
			return;
		}

		_expandedVisualScales.Add(
			hero,
			hero.VisualRoot.Scale);
	}

	private void OnExpandedChanged(bool isExpanded)
	{
		ApplyCurrentScale();
	}

	private void OnPlacementSettingsChanged()
	{
		ApplyCurrentScale();
	}

	private void ApplyCurrentScale()
	{
		float presentationScale =
			GetCurrentPresentationScale();

		foreach ((HeroActorController hero, Vector2 expandedScale)
			in _expandedVisualScales)
		{
			if (!GodotObject.IsInstanceValid(hero.VisualRoot))
				continue;

			hero.VisualRoot.Scale =
				expandedScale * presentationScale;
		}

		GD.Print(
			$"Hero presentation scale changed. " +
			$"Expanded={WindowHost.IsExpanded}, " +
			$"Scale={presentationScale:0.###}, " +
			$"Heroes={_expandedVisualScales.Count}");
	}

	private float GetCurrentPresentationScale()
	{
		if (WindowHost.IsExpanded)
			return 1.0f;

		float collapsedHeight = Mathf.Max(
			WindowHost.PlacementSettings.CollapsedHeight,
			1);

		float expandedHeight = Mathf.Max(
			WindowHost.PlacementSettings.ExpandedHeight,
			collapsedHeight);

		return Mathf.Clamp(
			collapsedHeight / expandedHeight,
			1.0f,
			1.0f);
	}

	private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(
			ActorLayer,
			nameof(ActorLayer));

		valid &= Require(
			WindowHost,
			nameof(WindowHost));

		return valid;
	}

	private static bool Require(
		GodotObject value,
		string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"HeroScalePresentationController is missing " +
			$"'{propertyName}'.");

		return false;
	}
}
