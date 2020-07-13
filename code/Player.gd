extends KinematicBody2D

var SPEED = 800
const SCREEN_DIV = 15
var screen_size
export (int, 0, 1) var player_num #left player = 0, right player = 1
var size
var playable = false
var curr_direction = 0
var prev_direction = 0
var client
var other

func _ready():
	screen_size = get_viewport_rect().size
	position.x = (screen_size.x / SCREEN_DIV)
	position.x += position.x * (SCREEN_DIV - 2) * player_num
	position.y = screen_size.y / 2
	size = $CollisionShape2D.get_shape().get_extents().y

func setup(playable_player, game_client, other_player):
	playable = (playable_player == player_num)
	client = game_client
	other = other_player + 1
	if (!playable):
		client.connect("PlayerDirection", self, "_on_direction_change")

func _on_direction_change(playerMovement):
	curr_direction = playerMovement[0]
	position.y = playerMovement[1]
	if (curr_direction != 0):
		var dif = ((get_parent().get_curr_tick() - playerMovement[2]) * ((SPEED / 1000) + 0.5)) * curr_direction
		position.y += dif

func _physics_process(delta):
	if playable:
		if (Input.is_action_pressed("player_up")):
			curr_direction = -1
		elif (Input.is_action_pressed("player_down")):
			curr_direction = 1
		else:
			curr_direction = 0
		if (prev_direction != curr_direction):
			prev_direction = curr_direction
			client.SendMessage(true, 116, str(curr_direction) + ":" + str(position.y) + ":" + str(get_parent().get_curr_tick()), other)
			
	move_and_collide(Vector2(0, curr_direction * SPEED) * delta)	

func is_playable():
	return playable

func get_position():
	return position


func get_size():
	return size
