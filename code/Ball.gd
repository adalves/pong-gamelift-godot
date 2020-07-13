#KNOWN BUGS
#Ball can get stuck between racket and wall. Maybe disable the racket collider until the ball hits something else?
extends KinematicBody2D

signal hit_score(side_hit)

const SPEED = 17
const MAX_ANGLE = 0.55
var screen_size
var collision
var velocity = Vector2()
var size
var client
var other
var point = false;

func _ready():
	screen_size = get_viewport_rect().size
	size = $CollisionShape2D.get_shape().get_extents().y
	set_ball()

func set_client(game_client, other_player):
	client = game_client
	client.connect("BallCollision", self, "_on_collision_adjust")
	other = other_player + 1

func set_ball():
	point = false;
	velocity = Vector2(0,0)
	position.x = screen_size.x / 2
	position.y = screen_size.y / 2


func initial_throw(direction_x, direction_y):
	set_ball()
	velocity.x = direction_x
	velocity.y = direction_y
	velocity = velocity.normalized() * SPEED

func _on_collision_adjust(data):
	position.x = data[2]
	position.y = data[3]
	velocity.x = data[0]
	velocity.y = data[1]
	print (str(velocity.x) + ":" + str(velocity.y) + ":" + str(position.x) + ":" + str(position.y))

func _physics_process(delta):
	if velocity == Vector2(0,0):
		return
	collision = move_and_collide(velocity)
	
	if (!point && collision):
		if collision.get_collider().is_class("KinematicBody2D"):
			var collision_pos = _racket_collision_pos(collision.get_collider())
			if (collision_pos < 1 && collision_pos > -1):
				_change_direction(collision_pos)
			else:
				_bounce()
			if (collision.get_collider().is_playable()):
				client.SendMessage(true, 118, str(velocity.x) + ":" + str(velocity.y) + ":" + str(position.x) + ":" + str(position.y), other)
		elif collision.get_normal() == Vector2(-1, 0):
			emit_signal("hit_score", 0)
			point = true
		elif collision.get_normal() == Vector2(1, 0):
			emit_signal("hit_score", 1)
			point = true
		else:
			_bounce()


func _bounce():
	velocity = velocity.bounce(collision.normal)
	velocity = velocity.normalized() * SPEED


func _racket_collision_pos(var racket):
	var racket_pos = racket.get_position()
	var racket_size = racket.get_size()
	var racket_top = racket_pos.y - racket_size
	var racket_bottom = racket_pos.y + racket_size
	var pos = position.y
	
	if (pos < racket_top && pos + size > racket_top):
		pos += size
	elif(pos > racket_bottom && pos - size < racket_bottom):
		pos -= size
	return inverse_lerp(racket_pos.y, racket_bottom, pos)


func _change_direction(var angle):
	var x_direction = sign(velocity.x) * -1
	var y_direction = sign(angle)
	angle = clamp(angle, -MAX_ANGLE, MAX_ANGLE)
	angle = abs(angle)
	
	velocity.x = (1 - angle) * x_direction
	velocity.y = angle * y_direction
	velocity = velocity.normalized() * SPEED
	
