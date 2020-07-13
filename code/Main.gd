extends Node

var playable
var other
var scores = [0, 0]
var start_time
onready var game_finish_popup = get_node("GameFinish/GameFinishPopup")
onready var hud = get_node("GameFinish/HUD")
onready var client = get_node("../Client")
const MAX_SCORES = 3

func _ready():
	client.connect("BallThrow", self, "_on_ball_throw")
	client.connect("GameFinish", self, "_on_game_finish")
	client.connect("PlayerDisconnected", self, "_on_player_disconnect")
	hud.set_invisible()
	
func match_ready():
	start_time = OS.get_ticks_msec()
	playable = get_parent().get_playable()
	if (playable == 0):
		other = 1
	else:
		other = 0
	scores = [0, 0]
	_setup_players()
	$Ball.set_client(client, other)
	hud.restart()

func _setup_players():
	$Player0.setup(playable, client, other)
	$Player1.setup(playable, client, other)
	
func _on_player_disconnect():
	client.Disconnect()
	get_tree().paused = true
	yield(_wait(),"completed")
	hud.set_invisible()
	game_finish_popup.get_node("MarginContainer/VBoxContainer/Label").text = "Disconnected"
	game_finish_popup.show()

func _on_ball_throw(new_scores):
	scores = new_scores
	hud.update_scores(scores)
	$Ball.set_ball()
	yield(_wait(), "completed")
	$Ball.initial_throw(-1, 0)
	
func _on_Ball_hit_score(side_hit):
	scores[side_hit] += 1
	if (side_hit != playable):
		client.SendMessage(false, 117, str(side_hit), -1)

func _wait():
	$ThrowTimer.start()
	yield ($ThrowTimer, "timeout")

func _on_game_finish(new_scores):
	client.Disconnect()
	scores = new_scores
	hud.update_scores(scores)
	$Ball.set_ball()
	get_tree().paused = true
	yield(_wait(),"completed")
	var game_end_str = ""
	if (scores[playable] == MAX_SCORES):
		game_end_str = "You win!"
	else:
		game_end_str = "You lose"
	game_finish_popup.get_node("MarginContainer/VBoxContainer/Label").text = game_end_str
	game_finish_popup.show()
	
func get_other():
	return other
	
func get_curr_tick():
	return OS.get_ticks_msec() - start_time
