using Godot;

[Tool]
[GlobalClass]
public partial class BodyBounds2D : Node2D
{
    private Vector2 _boundsSize = new(40.0f, 32.0f);

    [ExportCategory("Body Template")]

    [Export]
    public Vector2 BoundsSize
    {
        get => _boundsSize;
        set
        {
            _boundsSize = new Vector2(
                Mathf.Max(1.0f, value.X),
                Mathf.Max(1.0f, value.Y));

            QueueRedraw();
        }
    }

    public float GetHorizontalRadiusInParentSpace()
    {
        float halfWidth = BoundsSize.X * 0.5f;
        float height = BoundsSize.Y;

        Vector2[] corners =
        {
            new(-halfWidth, -height),
            new(halfWidth, -height),
            new(-halfWidth, 0.0f),
            new(halfWidth, 0.0f)
        };

        float radius = 0.0f;

        foreach (Vector2 corner in corners)
        {
            Vector2 transformedCorner =
                Transform * corner;

            radius = Mathf.Max(
                radius,
                Mathf.Abs(transformedCorner.X));
        }

        return radius;
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint())
            return;

        Rect2 boundsRectangle = new(
            new Vector2(
                -BoundsSize.X * 0.5f,
                -BoundsSize.Y),
            BoundsSize);

        DrawRect(
            boundsRectangle,
            new Color(0.15f, 0.75f, 1.0f, 0.12f),
            true);

        DrawRect(
            boundsRectangle,
            new Color(0.15f, 0.75f, 1.0f, 0.9f),
            false,
            1.0f);

        DrawLine(
            Vector2.Left * 4.0f,
            Vector2.Right * 4.0f,
            new Color(0.15f, 0.75f, 1.0f, 0.9f),
            1.0f);
    }
}