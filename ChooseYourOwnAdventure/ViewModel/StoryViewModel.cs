﻿using System;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ChooseYourOwnAdventure.Model;
using ChooseYourOwnAdventure.Service;
using InkRuntime = Ink.Runtime;

namespace ChooseYourOwnAdventure.ViewModel
{
	[QueryProperty ("StoryEntry", "StoryEntry")]
	public class StoryViewModel : BaseViewModel
	{
		StoryService storyService;
		StoryEntry storyEntry;
		InkRuntime.Story story;
		bool isChoosing;
		public StoryEntry StoryEntry {
			get => storyEntry;
			set {
				if (value is null)
					return;
				storyEntry = value;
				if (story is not null)
					return;
				App.Current.Dispatcher.Dispatch (async () => await LoadStory (storyEntry));
			}
		}

		public ObservableCollection<Line> Lines { get; } = new ObservableCollection<Line> ();

		public IEnumerable<InkRuntime.Choice> Choices => GetChoices();
		public bool IsChoosing {
			get => isChoosing;
			set {
				if (isChoosing == value)
					return;
				isChoosing = value;
				OnPropertyChanged();
			}
		}

		public bool IsComplete { get => !story?.canContinue ?? false; }
		public bool HasChoices { get => story?.currentChoices.Count > 0; }

		public ICommand Choose { get; private set; }
		public ICommand Restart { get; private set; }
		public ICommand ShowChoices { get; private set; }

		public StoryViewModel(StoryService service)
		{
			storyService = service;
			Choose = new Command<InkRuntime.Choice>((c) => {
				IsBusy = true;
				try
				{
					if (c is null)
						return;
					IsChoosing = false;
					story.ChooseChoiceIndex(c.index);
					ReadLines();
					OnPropertyChanged(nameof(Choices));
				} finally
				{
					IsBusy = false;
				}
			});
			Restart = new Command(() => {
				IsBusy = true;
				try
				{
					story?.ResetState();
					Lines.Clear();
					RemoveSaveState();
					IsChoosing = false;
					ReadLines();
				} finally
				{
					IsBusy = false;
				}
			});
			ShowChoices = new Command(() => {
				IsChoosing = HasChoices;
			});
		}

		private async Task<bool> LoadStory(StoryEntry entry)
		{
			try
			{
				IsBusy = true;
				story = await storyService.GetStory(entry);
				LoadState();
				ReadLines();
				return true;
			} finally
			{
				IsBusy = false;
			}
		}

		void ReadLines ()
		{
			if (story is null)
			{
				Lines.Add(new Line() { Text = "There was an error loading this story. Please try again later." });
				OnPropertyChanged(nameof(IsComplete));
				return;
			}
			
			while (story.canContinue)
			{
					Lines.Add(new Line() { Text = story.Continue(), Image = GetImageTag() });
			}
			if (story.currentChoices.Count > 0)
			{
				OnPropertyChanged(nameof(Choices));
				OnPropertyChanged(nameof(HasChoices));
				IsChoosing = true;
			}
			OnPropertyChanged(nameof(IsComplete));
		}

		IEnumerable<InkRuntime.Choice> GetChoices()
		{
			if (story is null)
			{
				yield return null;
			}
			else
			{
				foreach (var choice in story.currentChoices)
				{
					yield return choice;
				}
			}
		}

		string GetImageTag ()
		{
			foreach (var tag in story.currentTags)
			{
				if (tag.StartsWith ("image:"))
				{
					string image = tag.Replace("image:", string.Empty).Trim();
					if (!image.EndsWith (".png"))
						image += ".png";
					return image;
				}
			}
			return String.Empty;
		}

		void SaveState ()
		{
			string path = Path.Combine(FileSystem.Current.AppDataDirectory, "Saves", Path.GetFileName(storyEntry.StoryFile));
			Directory.CreateDirectory(Path.GetDirectoryName(path));

			var lineData = JsonSerializer.Serialize<ObservableCollection<Line>>(Lines);
			File.WriteAllText(Path.ChangeExtension (path, "dat"), lineData);
			string state = story.state.ToJson();
			File.WriteAllText(path, state);
		}

		void RemoveSaveState ()
		{
			string path = Path.Combine(FileSystem.Current.AppDataDirectory, "Saves", Path.GetFileName(storyEntry.StoryFile));
			if (File.Exists(path))
				File.Delete(path);
			if (File.Exists (Path.ChangeExtension(path, "dat")))
				File.Delete(Path.ChangeExtension(path, "dat"));
		}

		void LoadState ()
		{
			string path = Path.Combine(FileSystem.Current.AppDataDirectory, "Saves", Path.GetFileName (storyEntry.StoryFile));
			if (!File.Exists(path))
				return;
			string lineData = Path.ChangeExtension(path, "dat");
			if (File.Exists(lineData))
			{
				var data = JsonSerializer.Deserialize<ObservableCollection<Line>>(File.ReadAllText(lineData));
				foreach (var l in data)
					Lines.Add(l);

			}	
			string json = File.ReadAllText(path);
			try
			{
				story.state.LoadJson(json);
			} catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex);
			}
		}

		public void Closing()
		{
			// we are closing , we need to save the state of the story.
			SaveState();
		}
	}
}

