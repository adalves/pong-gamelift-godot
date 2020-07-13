extends Button

func _on_Option_pressed():
	get_tree().change_scene("res://scenes/menu/MainMenu.tscn")
	get_tree().paused = false
