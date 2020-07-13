extends Label

const AMPLITUDE = 5
var period = 0.1
var start_pos
var total_time

func _ready():
	start_pos = rect_position
	randomize()
	period = rand_range(0.05, 0.15)
	total_time = 0

func _process(delta):
	var theta = total_time / period
	var distance = AMPLITUDE * sin(theta)
	rect_position = start_pos + Vector2(0, 1) * distance
	total_time += delta
