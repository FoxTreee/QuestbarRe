@tool
extends EditorProperty


func configure(description: String) -> void:
	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.11, 0.14, 0.18, 0.72)
	style.border_color = Color(0.25, 0.49, 0.68, 0.8)
	style.set_border_width_all(1)
	style.set_corner_radius_all(4)
	style.content_margin_left = 9.0
	style.content_margin_top = 6.0
	style.content_margin_right = 9.0
	style.content_margin_bottom = 6.0
	panel.add_theme_stylebox_override("panel", style)

	var label := Label.new()
	label.text = description
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.add_theme_color_override("font_color", Color(0.78, 0.86, 0.94))
	label.add_theme_font_size_override("font_size", 12)
	label.tooltip_text = description
	panel.add_child(label)

	add_child(panel)
	set_bottom_editor(panel)

