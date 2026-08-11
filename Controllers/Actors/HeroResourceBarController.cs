using Godot;

public partial class HeroResourceBarController : Node2D
{
	private static readonly Color ManaColor =
		new("3f7cff");
	private static readonly Color EnergyColor =
		new("f2cf3a");
	private static readonly Color RageColor =
		new("d94a4a");

	private ProgressBar _resourceProgress = null!;
	private HeroResourceState? _resource;
	private HeroResourceType _displayedType =
		HeroResourceType.None;

	/// <summary>
	/// Finds the required progress bar and begins hidden. The owning hero binds
	/// its runtime resource state after both nodes have entered the scene tree.
	/// </summary>
	public override void _Ready()
	{
		_resourceProgress =
			GetNodeOrNull<ProgressBar>("ResourceProgress")!;

		if (!GodotObject.IsInstanceValid(_resourceProgress))
		{
			GD.PushError(
				$"{Name} requires a ProgressBar child named " +
				"'ResourceProgress'.");
		}

		Visible = false;
	}

	/// <summary>
	/// Connects this presentation to one hero's generic resource state. The bar
	/// remains hidden while that state represents the None resource type.
	/// </summary>
	public void Bind(HeroResourceState resource)
	{
		_resource = resource
			?? throw new System.ArgumentNullException(
				nameof(resource));

		Refresh();
	}

	/// <summary>
	/// Refreshes visibility, fill amount, capacity, and resource color every
	/// frame so spending, regeneration, and debug type changes appear at once.
	/// </summary>
	public override void _Process(double delta)
	{
		Refresh();
	}

	/// <summary>
	/// Applies the bound resource state to the Godot controls. A missing or None
	/// resource hides the entire bar instead of displaying an empty meter.
	/// </summary>
	private void Refresh()
	{
		if (!GodotObject.IsInstanceValid(_resourceProgress)
			|| _resource is null
			|| !_resource.HasResource)
		{
			Visible = false;
			_displayedType = HeroResourceType.None;
			return;
		}

		Visible = true;
		_resourceProgress.MaxValue =
			Mathf.Max(_resource.MaximumAmount, 1.0f);
		_resourceProgress.Value =
			Mathf.Clamp(
				_resource.CurrentAmount,
				0.0f,
				_resource.MaximumAmount);

		if (_displayedType == _resource.ResourceType)
			return;

		_displayedType = _resource.ResourceType;
		ApplyFillColor(GetResourceColor(_displayedType));
	}

	/// <summary>
	/// Duplicates the fill style before recoloring it so each hero can display
	/// a different resource color without mutating a shared scene resource.
	/// </summary>
	private void ApplyFillColor(Color color)
	{
		StyleBoxFlat fillStyle =
			_resourceProgress.GetThemeStylebox("fill")
				.Duplicate() as StyleBoxFlat
			?? new StyleBoxFlat();

		fillStyle.BgColor = color;
		_resourceProgress.AddThemeStyleboxOverride(
			"fill",
			fillStyle);
	}

	/// <summary>
	/// Maps the generic resource type to Questbar's presentation palette:
	/// Mana is blue, Energy is yellow, and Rage is red.
	/// </summary>
	private static Color GetResourceColor(
		HeroResourceType resourceType)
	{
		return resourceType switch
		{
			HeroResourceType.Mana => ManaColor,
			HeroResourceType.Energy => EnergyColor,
			HeroResourceType.Rage => RageColor,
			_ => Colors.White
		};
	}
}
