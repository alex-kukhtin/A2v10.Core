// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Xaml;

public class SwitchBox : CheckBoxBase
{
	internal override String ControlType => "switchbox";
	internal override String InputControlType => "checkbox";
	internal override String? InputControlClass => "switch";
}
