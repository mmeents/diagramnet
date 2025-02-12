﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagramNet {

	public enum DesignerAction {
		Select,
		Add,
		Delete,
		Connect
	}

	internal enum CornerPosition : int {
		Nothing = -1,
		BottomCenter = 0,
		BottomLeft = 1,
		BottomRight = 2,
		MiddleCenter = 3,
		MiddleLeft = 4,
		MiddleRight = 5,
		TopCenter = 6,
		TopLeft = 7,
		TopRight = 8,
		Undefined = 99
	}

	public enum CardinalDirection {
		Nothing,
		North,
		South,
		East,
		West
	}

	public enum Orientation {
		Horizontal,
		Vertical
	}

	public enum ElementType {
		Rectangle,
		RectangleNode,
		Elipse,
		ElipseNode,
		CommentBox,
		CommentBoxNode

	}

	public enum LinkType {
		Straight,
		RightAngle
	}

	internal enum LabelEditDirection {
		UpDown,
		Both
	}

}
