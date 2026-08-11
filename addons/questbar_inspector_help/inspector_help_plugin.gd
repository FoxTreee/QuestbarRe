@tool
extends EditorInspectorPlugin

const DESCRIPTION_PATH := "res://addons/questbar_inspector_help/property_descriptions.json"
const HELP_EDITOR := preload("res://addons/questbar_inspector_help/property_help.gd")

var _descriptions: Dictionary = {}
func _init() -> void:
	_load_descriptions()


func _can_handle(object: Object) -> bool:
	return _descriptions.has(_get_script_path(object))


func _parse_property(
	object: Object,
	_type: Variant.Type,
	name: String,
	_hint_type: PropertyHint,
	_hint_string: String,
	_usage_flags: int,
	_wide: bool
) -> bool:
	var script_descriptions: Dictionary = _descriptions.get(_get_script_path(object), {})
	var description := str(script_descriptions.get(name, ""))
	if description.is_empty():
		return false

	var help_editor := HELP_EDITOR.new()
	help_editor.configure(description)
	add_property_editor(name, help_editor, true)
	return false


func _get_script_path(object: Object) -> String:
	if object == null:
		return ""
	var script: Script = object.get_script()
	if script == null:
		return ""
	return script.resource_path


func _load_descriptions() -> void:
	if not FileAccess.file_exists(DESCRIPTION_PATH):
		push_warning("Questbar Inspector Help could not find property_descriptions.json.")
		return

	var file := FileAccess.open(DESCRIPTION_PATH, FileAccess.READ)
	if file == null:
		push_warning("Questbar Inspector Help could not open property_descriptions.json.")
		return

	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if parsed is Dictionary:
		_descriptions = parsed
	else:
		push_warning("Questbar Inspector Help found invalid JSON in property_descriptions.json.")
