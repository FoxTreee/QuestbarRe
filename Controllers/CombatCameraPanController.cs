using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class CombatCameraPanController : Camera2D
{
	[ExportCategory("Combat Pan")]
	/// <summary>
	/// Controls how far the camera pans horizontally when an encounter begins.
	/// Negative values pan the view left and make the battle appear farther right.
	/// </summary>
	[Export(PropertyHint.Range, "-360,160,1")]
	public float CombatPanOffsetX { get; set; } = -80.0f;

	/// <summary>
	/// Controls how long combat runs before the camera starts moving.
	/// Use this to let the heroes and monsters begin closing distance first.
	/// </summary>
	[Export(PropertyHint.Range, "0,3,0.05")]
	public float PanStartDelay { get; set; } = 1.5f;

	/// <summary>
	/// Controls how many seconds the camera takes to reach its new framing.
	/// Smaller values feel snappier; larger values create a slower cinematic pan.
	/// </summary>
	[Export(PropertyHint.Range, "0.05,6,0.05")]
	public float PanDuration { get; set; } = 1.25f;

	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used to detect encounter and travel transitions.
	/// Assign the main JourneyStateService node from the scene.
	/// </summary>
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	/// <summary>
	/// Inspector reference used to detect when the heroes finish returning home.
	/// Assign the main PartyController node from the scene.
	/// </summary>
	[Export]
	public PartyController Party { get; set; } = null!;

	/// <summary>
	/// Inspector reference used to suppress automatic parallax camera movement
	/// only while the party and camera perform their synchronized handoff.
	/// </summary>
	[Export]
	public Parallax2D TravelingGround { get; set; } = null!;

	private Vector2 _travelPosition;
	private Tween? _activePan;
	private bool _waitingForPartyFormation;
	private float _heldCameraOffsetX;
	private float _groundCompensationApplied;
	private readonly Dictionary<HeroActorController, Vector2>
		_baseFormationOffsets = new();

	/// <summary>
	/// Stores the Camera2D's authored position as the normal travel framing.
	/// It then follows journey-state changes without moving gameplay actors.
	/// </summary>
	public override void _Ready()
	{
		if (!GodotObject.IsInstanceValid(JourneyState)
			|| !GodotObject.IsInstanceValid(Party)
			|| !GodotObject.IsInstanceValid(TravelingGround))
		{
			GD.PushError(
				"CombatCameraPanController requires JourneyState, Party, " +
				"and TravelingGround Inspector references.");
			SetProcess(false);
			return;
		}

		_travelPosition = Position;
		TravelingGround.IgnoreCameraScroll = false;
		JourneyState.StateChanged += OnJourneyStateChanged;
		ApplyStateImmediately(JourneyState.CurrentState);
	}

	/// <summary>
	/// Holds the combat framing while the heroes return to their authored anchors.
	/// Travel presentation continues normally during this positional check.
	/// </summary>
	public override void _Process(double delta)
	{
		if (!_waitingForPartyFormation)
		{
			SetProcess(false);
			return;
		}

		if (!IsAvailablePartyInFormation())
			return;

		_waitingForPartyFormation = false;
		SetProcess(false);
		StartTravelFollowPan();
	}

	/// <summary>
	/// Disconnects the journey-state listener and stops any active camera tween.
	/// This prevents callbacks from surviving after the camera leaves the scene.
	/// </summary>
	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
			JourneyState.StateChanged -= OnJourneyStateChanged;

		_activePan?.Kill();
		_activePan = null;

	}

	/// <summary>
	/// Starts a smooth pan when combat begins and restores travel framing when it ends.
	/// Non-travel states retain combat framing so defeat prompts do not jerk the view.
	/// </summary>
	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		_activePan?.Kill();

		if (currentState == JourneyStateService.JourneyState.Traveling)
		{
			_heldCameraOffsetX = Position.X - _travelPosition.X;
			ApplyCameraRelativeFormationTargets();
			_waitingForPartyFormation = true;
			SetProcess(true);
			DebugLog.Print(
				"Combat camera is holding its offset until the party " +
				"returns to formation.");
			return;
		}

		_waitingForPartyFormation = false;
		SetProcess(false);
		if (currentState == JourneyStateService.JourneyState.Encounter)
		{
			CaptureBaseFormationOffsets();
			RestoreBaseFormationOffsets();
		}

		StartPan(GetTargetPosition(currentState), true);

		DebugLog.Print(
			$"Combat camera pan: {previousState} → {currentState}, " +
			$"Target={GetTargetPosition(currentState)}, " +
			$"Delay={PanStartDelay:0.##}s, " +
			$"Duration={PanDuration:0.##}s.");
	}

	/// <summary>
	/// Starts a camera tween toward the requested framing, optionally applying
	/// the authored combat entrance delay before movement begins.
	/// </summary>
	private void StartPan(Vector2 targetPosition, bool useStartDelay)
	{
		_activePan?.Kill();
		_activePan = CreateTween()
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.InOut);

		if (useStartDelay && PanStartDelay > 0.0f)
			_activePan.TweenInterval(PanStartDelay);

		_activePan.TweenProperty(
			this,
			"position",
			targetPosition,
			Mathf.Max(PanDuration, 0.01f));
	}

	/// <summary>
	/// Moves the camera and party back to normal travel coordinates together.
	/// Equal world-space movement keeps every hero stable relative to the camera.
	/// </summary>
	private void StartTravelFollowPan()
	{
		_activePan?.Kill();
		_groundCompensationApplied = 0.0f;
		_activePan = CreateTween()
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.InOut)
			.SetParallel(true);

		double duration = Mathf.Max(PanDuration, 0.01f);
		_activePan.TweenProperty(
			this,
			"position",
			_travelPosition,
			duration);

		float cameraMovementX =
			_travelPosition.X - Position.X;

		_activePan.TweenMethod(
			Callable.From<float>(ApplyGroundCompensation),
			0.0f,
			cameraMovementX,
			duration);

		foreach (HeroActorController hero in GetAvailableHeroes())
		{
			Vector2 baseOffset = GetBaseFormationOffset(hero);
			hero.FormationOffset = baseOffset;

			_activePan.TweenProperty(
				hero,
				"global_position",
				hero.FormationPosition,
				duration);
		}

		DebugLog.Print(
			"Party reached camera-relative travel formation. " +
			"Camera follow resumed without changing screen-space formation.");
	}

	/// <summary>
	/// Adds only the camera handoff's incremental displacement to the ground.
	/// Normal travel scrolling remains owned by RegionPresentationController.
	/// </summary>
	private void ApplyGroundCompensation(float cumulativeOffsetX)
	{
		float frameOffsetX =
			cumulativeOffsetX - _groundCompensationApplied;

		_groundCompensationApplied = cumulativeOffsetX;
		TravelingGround.ScrollOffset +=
			Vector2.Right * frameOffsetX;
	}

	/// <summary>
	/// Records each hero's authored runtime formation offset once. Existing
	/// entries are never overwritten because a rapid encounter can begin while
	/// the hero is still using a temporary camera-relative formation target.
	/// </summary>
	private void CaptureBaseFormationOffsets()
	{
		foreach (HeroActorController hero in GetAvailableHeroes())
		{
			if (!_baseFormationOffsets.ContainsKey(hero))
				_baseFormationOffsets[hero] = hero.FormationOffset;
		}
	}

	/// <summary>
	/// Restores canonical formation targets when a new encounter interrupts the
	/// return-to-travel handoff. Heroes keep their current world positions and
	/// may immediately engage the new monsters without adopting the temporary
	/// camera-relative offset as their permanent home position.
	/// </summary>
	private void RestoreBaseFormationOffsets()
	{
		foreach (HeroActorController hero in GetAvailableHeroes())
			hero.FormationOffset = GetBaseFormationOffset(hero);
	}

	/// <summary>
	/// Shifts formation targets by the active camera displacement so heroes walk
	/// left into their normal on-screen travel positions while the camera holds.
	/// </summary>
	private void ApplyCameraRelativeFormationTargets()
	{
		foreach (HeroActorController hero in GetAvailableHeroes())
		{
			Vector2 baseOffset = GetBaseFormationOffset(hero);
			hero.FormationOffset =
				baseOffset + Vector2.Right * _heldCameraOffsetX;
		}
	}

	/// <summary>
	/// Returns the stored authored offset, capturing the current value as a safe
	/// fallback when a hero joined after combat began.
	/// </summary>
	private Vector2 GetBaseFormationOffset(HeroActorController hero)
	{
		if (_baseFormationOffsets.TryGetValue(hero, out Vector2 offset))
			return offset;

		offset = hero.FormationOffset;
		_baseFormationOffsets[hero] = offset;
		return offset;
	}

	/// <summary>
	/// Returns the valid, non-incapacitated heroes participating in formation.
	/// </summary>
	private IEnumerable<HeroActorController> GetAvailableHeroes()
	{
		return Party.SpawnedHeroes.Where(
			hero => GodotObject.IsInstanceValid(hero)
				&& !hero.IsIncapacitated);
	}

	/// <summary>
	/// Returns true once every non-incapacitated hero has reached formation.
	/// Incapacitated heroes do not block the camera from restoring travel framing.
	/// </summary>
	private bool IsAvailablePartyInFormation()
	{
		return GetAvailableHeroes()
			.All(hero =>
				hero.GlobalPosition.DistanceTo(hero.FormationPosition)
					<= hero.CombatArrivalDistance);
	}

	/// <summary>
	/// Applies the correct framing at startup without playing an entrance tween.
	/// This also supports testing with the initial journey state set to Encounter.
	/// </summary>
	private void ApplyStateImmediately(
		JourneyStateService.JourneyState state)
	{
		Position = GetTargetPosition(state);
	}

	/// <summary>
	/// Returns travel framing only while traveling; every encounter-related state
	/// keeps the combat offset until the journey explicitly resumes travel.
	/// </summary>
	private Vector2 GetTargetPosition(
		JourneyStateService.JourneyState state)
	{
		return state == JourneyStateService.JourneyState.Traveling
			? _travelPosition
			: _travelPosition + Vector2.Right * CombatPanOffsetX;
	}
}
