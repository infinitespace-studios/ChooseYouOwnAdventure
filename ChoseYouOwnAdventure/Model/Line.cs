﻿using System;
namespace ChoseYouOwnAdventure.Model
{
	public class Line
	{
		public string Text { get; set; }
		public string Image { get; set; }

		public bool IsText => string.IsNullOrEmpty(Text);
		public bool IsImage => !IsText;
	}
}

