﻿// Copyright © 2018-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml.Drawing;

public interface IHasPositionAndSize
{
	Size? Size { get; }
	Point? Pos { get; }
}

public interface IHasMarkers
{
	LineMarkerStyle MarkerStart { get; }
	LineMarkerStyle MarkerEnd { get; }
}
