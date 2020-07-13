extends CanvasLayer

func restart():	
	update_scores([0, 0])
	_set_visible()

func update_scores(scores):
	get_node("Player0Score").text = str(scores[0])
	get_node("Player1Score").text = str(scores[1])
	
func set_invisible():
	get_node("Player0Score").hide()
	get_node("Player1Score").hide()
	
func _set_visible():
	get_node("Player0Score").show()
	get_node("Player1Score").show()
