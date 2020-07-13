extends Node

var thread
var playable_player = 0
var main

func _ready():
	$Client.connect("PlayerNumber", self, "_on_PlayerNumber_set")
	thread = Thread.new()
	thread.start(self, "search_for_match")

func search_for_match(userdata):
	$Client.SearchGameSessions()
	while(!$Client.IsConnected()):
		pass
	call_deferred("_add_main")
		
func _add_main():
	print("match found, loading game")
	main = preload("res://scenes/Game.tscn").instance()
	add_child(main)
	move_child(main, 1)

func _exit_tree():
	thread.wait_to_finish()
	
func _on_PlayerNumber_set(number):
	playable_player = number
	_setup_match()
	
func _setup_match():
	$SearchingScreen.queue_free()
	$Main.match_ready()

func get_playable():
	return playable_player
	
