using Godot;

/// <summary>
/// Editor-authored fog opening placed over a road, landmark, or destination.
/// It is invisible to players; the map presenter sends its position, radius,
/// and travel milestone to the fog shader at runtime.
/// </summary>
[Tool]
[GlobalClass]
public partial class RegionMapFogRevealArea : Node2D
{
	private float _revealAtTravelSeconds;
	private float _growDurationSeconds = 30.0f;
	private float _radiusPixels = 72.0f;
	private float _featherPixels = 18.0f;

	[ExportCategory("Exploration")]

	/// <summary>
	/// Traveling-state seconds required before this fog opening begins.
	/// </summary>
	[Export(PropertyHint.Range, "0,86400,1,suffix:s")]
	public float RevealAtTravelSeconds
	{
		get => _revealAtTravelSeconds;
		set
		{
			_revealAtTravelSeconds = Mathf.Max(0.0f, value);
			QueueRedraw();
		}
	}

	/// <summary>
	/// Seconds spent expanding from the center to Radius Pixels. Set to zero
	/// for an immediate milestone reveal.
	/// </summary>
	[Export(PropertyHint.Range, "0,3600,1,suffix:s")]
	public float GrowDurationSeconds
	{
		get => _growDurationSeconds;
		set => _growDurationSeconds = Mathf.Max(0.0f, value);
	}

	[ExportCategory("Shape")]

	/// <summary>
	/// Final radius of the revealed area in the map's 404-by-745 pixel space.
	/// </summary>
	[Export(PropertyHint.Range, "8,512,1,suffix:px")]
	public float RadiusPixels
	{
		get => _radiusPixels;
		set
		{
			_radiusPixels = Mathf.Max(8.0f, value);
			QueueRedraw();
		}
	}

	/// <summary>
	/// Softness of the fog boundary in pixels.
	/// </summary>
	[Export(PropertyHint.Range, "0,128,1,suffix:px")]
	public float FeatherPixels
	{
		get => _featherPixels;
		set
		{
			_featherPixels = Mathf.Max(0.0f, value);
			QueueRedraw();
		}
	}

	[ExportCategory("Authoring")]

	[Export(PropertyHint.MultilineText)]
	public string DesignerNotes { get; set; } = string.Empty;

	/// <summary>
	/// Draws the authored radius only in Godot's editor. Runtime players see
	/// the resulting shader opening, never this cyan guide.
	/// </summary>
	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;

		Color guideColor = new(0.1f, 0.8f, 1.0f, 0.75f);
		DrawCircle(Vector2.Zero, 4.0f, guideColor);
		DrawArc(
			Vector2.Zero,
			RadiusPixels,
			0.0f,
			Mathf.Tau,
			64,
			guideColor,
			1.5f);
	}

	/// <summary>
	/// Calculates the currently revealed radius from normal travel progress.
	/// </summary>
	public float GetCurrentRadius(double accumulatedTravelSeconds)
	{
		if (accumulatedTravelSeconds < RevealAtTravelSeconds)
			return 0.0f;

		if (GrowDurationSeconds <= 0.0f)
			return RadiusPixels;

		float growth = Mathf.Clamp(
			(float)((accumulatedTravelSeconds - RevealAtTravelSeconds)
				/ GrowDurationSeconds),
			0.0f,
			1.0f);

		return RadiusPixels * growth;
	}
}
