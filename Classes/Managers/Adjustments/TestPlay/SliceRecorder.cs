using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using EasyOffset.Configuration;
using static BeatmapSaveData;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace EasyOffset.TestPlay
{
	internal class Score
	{
		public float PreSwing { get; set; }
		public float PostSwing { get; set; }
		public float Offset { get; set; }
		public float TotalScore
		{
			get
			{
				return PreSwing + PostSwing + Offset;
			}
		}

		public Score(float preSwing, float postSwing, float offset)
		{
			PreSwing = preSwing;
			PostSwing = postSwing;
			Offset = offset;
		}

		public static Score operator +(Score a, Score b) => new Score(a.PreSwing + b.PreSwing, a.PostSwing + b.PostSwing, a.Offset + b.Offset);
		public static Score operator /(Score a, float b) => new Score(a.PreSwing / b, a.PostSwing / b, a.Offset / b);
	}

	internal class NoteInfo
	{
		public NoteData noteData;
		public NoteCutInfo cutInfo;
		public float cutAngle;
		public float cutOffset;
		public Score score;
		public Vector2 noteGridPosition;
		public int noteIndex;

		public NoteInfo()
		{

		}

		public NoteInfo(NoteData noteData, NoteCutInfo cutInfo, float cutAngle, float cutOffset, Vector2 noteGridPosition, int noteIndex)
		{
			this.noteData = noteData;
			this.cutInfo = cutInfo;
			this.cutAngle = cutAngle;
			this.cutOffset = cutOffset;
			this.noteGridPosition = noteGridPosition;
			this.noteIndex = noteIndex;
		}
	}

	class SliceRecorder: IInitializable, IDisposable, ISaberSwingRatingCounterDidFinishReceiver
    {
		public static SliceRecorder instance { get; private set; }

		private BeatmapObjectManager _beatmapObjectManager;

		private Dictionary<ISaberSwingRatingCounter, NoteInfo> _noteSwingInfos = new Dictionary<ISaberSwingRatingCounter, NoteInfo>();
		private List<NoteInfo> _noteInfos = new List<NoteInfo>();

		private float rightOffset = 0.1f;
		private int rightSelector = 0;
		private float leftOffset = 0.1f;
		private int leftSelector = 0;

		private float rightLastScore = 0;
		private float rightScore = 0;
		private int rightScoreCounter = 0;
		private float leftLastScore = 0;
		private float leftScore = 0;
		private int leftScoreCounter = 0;

		public SliceRecorder(BeatmapObjectManager beatmapObjectManager)
		{
			Plugin.Log.Critical("Test hui 1");
			_beatmapObjectManager = beatmapObjectManager;
		}

		public void Initialize()
		{
			Plugin.Log.Critical("Test hui 2");
			instance = this;
			_beatmapObjectManager.noteWasCutEvent += OnNoteWasCut;
			//SliceProcessor.instance.ResetProcessor();
		}

		public void Dispose()
		{
			_beatmapObjectManager.noteWasCutEvent -= OnNoteWasCut;
			// Process slices once the map ends
			ProcessSlices();
		}

		public void ProcessSlices()
		{
			//SliceProcessor.instance.ProcessSlices(_noteInfos);
		}

		private void OnNoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo)
		{
			if (noteController.noteData.colorType == ColorType.None || !noteCutInfo.allIsOK) return;
			ProcessNote(noteController, noteCutInfo);
		}

		private void ProcessNote(NoteController noteController, NoteCutInfo noteCutInfo)
		{
			if (noteController == null) return;

			Vector2 noteGridPosition;
			noteGridPosition.y = (int)noteController.noteData.noteLineLayer;
			noteGridPosition.x = noteController.noteData.lineIndex;
			int noteIndex = (int)(noteGridPosition.y * 4 + noteGridPosition.x);

			// No ME notes allowed >:(
			if (noteGridPosition.x >= 4 || noteGridPosition.y >= 3 || noteGridPosition.x < 0 || noteGridPosition.y < 0) return;

			Vector3 cutDirection = new Vector3(-noteCutInfo.cutNormal.y, noteCutInfo.cutNormal.x, 0f);
			float cutAngle = Mathf.Atan2(cutDirection.y, cutDirection.x) * Mathf.Rad2Deg + 90.0f;

			float cutOffset = noteCutInfo.cutDistanceToCenter;
			Vector3 noteCenter = noteController.noteTransform.position;
			if (Vector3.Dot(noteCutInfo.cutNormal, noteCutInfo.cutPoint - noteCenter) > 0f)
			{
				cutOffset = -cutOffset;
			}

			NoteInfo noteInfo = new NoteInfo(noteController.noteData, noteCutInfo, cutAngle, cutOffset, noteGridPosition, noteIndex);

			_noteSwingInfos.Add(noteCutInfo.swingRatingCounter, noteInfo);

			noteCutInfo.swingRatingCounter.UnregisterDidFinishReceiver(this);
			noteCutInfo.swingRatingCounter.RegisterDidFinishReceiver(this);
		}

		public void HandleSaberSwingRatingCounterDidFinish(ISaberSwingRatingCounter saberSwingRatingCounter)
		{
			NoteInfo noteSwingInfo;
			if (_noteSwingInfos.TryGetValue(saberSwingRatingCounter, out noteSwingInfo))
			{
				int preSwing, postSwing, offset;
				ScoreModel.RawScoreWithoutMultiplier(saberSwingRatingCounter, noteSwingInfo.cutInfo.cutDistanceToCenter, out preSwing, out postSwing, out offset);

				noteSwingInfo.score = new Score(preSwing, postSwing, offset);

				_noteInfos.Add(noteSwingInfo);
				_noteSwingInfos.Remove(saberSwingRatingCounter);

				AnalyzeNote(noteSwingInfo);
			}
			else
			{
				// Bad cut, do nothing
			}
		}

		private void applyRightOffset()
        {
			if (Math.Abs(rightOffset) < 0.04f)
            {
				rightOffset = 0.1f;
				rightSelector++;
				return;
            }

			if (rightSelector < 3)
            {
				Vector3 direction = PluginConfig.RightHandSaberDirection;
				switch (rightSelector)
				{
					case 0:
						direction.x += rightOffset;
						break;
					case 1:
						direction.y += rightOffset;
						break;
					case 2:
						direction.z += rightOffset;
						break;
				}

				PluginConfig.RightHandSaberDirection = direction;
			}
			else if(rightSelector < 6)
			{
				Vector3 direction = PluginConfig.RightHandPivotPosition;
				switch (rightSelector)
				{
					case 3:
						direction.x += rightOffset;
						break;
					case 4:
						direction.y += rightOffset;
						break;
					case 5:
						direction.z += rightOffset;
						break;
				}

				PluginConfig.RightHandPivotPosition = direction;
			} else if (rightSelector == 6)
			{
				//PluginConfig.RightHandZOffset += rightOffset;
			}
		}

		private void applyLeftOffset()
		{
			if (Math.Abs(leftOffset) < 0.04f)
			{
				leftOffset = 0.1f;
				leftSelector++;
				return;
			}

			if (leftSelector < 3)
			{
				Vector3 direction = PluginConfig.LeftHandSaberDirection;
				switch (leftSelector)
				{
					case 0:
						direction.x += leftOffset;
						break;
					case 1:
						direction.y += leftOffset;
						break;
					case 2:
						direction.z += leftOffset;
						break;
				}

				PluginConfig.LeftHandSaberDirection = direction;
			}
			else if (leftSelector < 6)
			{
				Vector3 direction = PluginConfig.LeftHandPivotPosition;
				switch (leftSelector)
				{
					case 3:
						direction.x += leftOffset;
						break;
					case 4:
						direction.y += leftOffset;
						break;
					case 5:
						direction.z += leftOffset;
						break;
				}

				PluginConfig.LeftHandPivotPosition = direction;
			} else if (leftSelector == 6)
            {
				//PluginConfig.LeftHandZOffset += leftOffset;
			}
		}

		private void AnalyzeNote(NoteInfo noteInfo)
        {
			if (noteInfo.noteData.colorType == ColorType.ColorA)
            {
				float score = noteInfo.score.TotalScore;
				if (leftScoreCounter < 4)
                {
					leftScore += score;
					leftScoreCounter++;
					return;
				}
				
				if (leftScore < leftLastScore)
				{
					leftOffset *= -1.1f;

					applyLeftOffset();
				}
				else
				{
					leftOffset *= 0.9f;
					applyLeftOffset();
				}

				leftLastScore = leftScore;
				leftScoreCounter = 0;

				Plugin.Log.Critical("Left cut " + leftLastScore / 5 + " " + leftOffset + " " + leftSelector + " " + noteInfo.noteData.lineIndex + " " + noteInfo.noteData.noteLineLayer + "\n" + CutToStringL(noteInfo.cutInfo));
			} else
            {
				float score = noteInfo.score.TotalScore;
				if (rightScoreCounter < 4)
				{
					rightScore += score;
					rightScoreCounter++;
					return;
				}

				if (rightScore < rightLastScore)
                {
					rightOffset *= -1.1f;

					applyRightOffset();
				} else
                {
					rightOffset *= 0.9f;
					applyRightOffset();
				}

				rightLastScore = rightScore;
				rightScoreCounter = 0;

				Plugin.Log.Critical("Right cut " + rightLastScore / 5 + " " + rightOffset + " " + rightSelector + " " + noteInfo.noteData.lineIndex + " " + noteInfo.noteData.noteLineLayer + "\n" + CutToString(noteInfo.cutInfo));
				//PluginConfig.RightHandSaberDirection = noteInfo.cutInfo.saberDir - PluginConfig.RightHandSaberDirection;
				//PluginConfig.RightHandSaberDirection += noteInfo.cutInfo.cutNormal;
				//PluginConfig.RightHandPivotPosition = newPivotPosition;
			}
        }

		private String CutToString(NoteCutInfo cutInfo)
        {
			//	public readonly bool speedOK;
			//public readonly ISaberSwingRatingCounter swingRatingCounter;
			//public readonly float cutDistanceToCenter;
			//public readonly float cutAngle;
			//public readonly Vector3 cutNormal;
			//public readonly float cutDirDeviation;
			//public readonly float timeDeviation;
			//public readonly Vector3 cutPoint;
			//public readonly Vector3 saberDir;
			//public readonly float saberSpeed;
			//public readonly bool wasCutTooSoon;
			//public readonly bool saberTypeOK;
			//public readonly bool directionOK;
			//public readonly SaberType saberType;
			return  
				//"\nspeedOK:" + cutInfo.speedOK +
				//	"\ndistToCentr:" + cutInfo.cutDistanceToCenter +
				//	"\ncutAngle:" + cutInfo.cutAngle +
				//	"\ncutNormal:" + cutInfo.cutNormal.x + " " + cutInfo.cutNormal.y + " " + cutInfo.cutNormal.z + " " +
				//	"\ncutDirDeviation:" + cutInfo.cutDirDeviation +
				//	"\ntimeDeviation:" + cutInfo.timeDeviation +
				//	"\nsaberSpeed:" + cutInfo.saberSpeed +
				//	"\nwasCutTooSoon:" + cutInfo.wasCutTooSoon +
				//	"\ncutPoint:" + cutInfo.cutPoint.x + " " + cutInfo.cutPoint.y + " " + cutInfo.cutPoint.z + " " +
				//	"\nsaberDir:" + cutInfo.saberDir.x + " " + cutInfo.saberDir.y + " " + cutInfo.saberDir.z + " " +
					"\nconfig:" + PluginConfig.RightHandSaberDirection.x + " " + PluginConfig.RightHandSaberDirection.y + " " + PluginConfig.RightHandSaberDirection.z + " \n\n";

		}

		private String CutToStringL(NoteCutInfo cutInfo)
		{
			//	public readonly bool speedOK;
			//public readonly ISaberSwingRatingCounter swingRatingCounter;
			//public readonly float cutDistanceToCenter;
			//public readonly float cutAngle;
			//public readonly Vector3 cutNormal;
			//public readonly float cutDirDeviation;
			//public readonly float timeDeviation;
			//public readonly Vector3 cutPoint;
			//public readonly Vector3 saberDir;
			//public readonly float saberSpeed;
			//public readonly bool wasCutTooSoon;
			//public readonly bool saberTypeOK;
			//public readonly bool directionOK;
			//public readonly SaberType saberType;
			return 
				//"speedOK:" + cutInfo.speedOK +
				//	"\ndistToCentr:" + cutInfo.cutDistanceToCenter +
				//	"\ncutAngle:" + cutInfo.cutAngle +
				//	"\ncutNormal:" + cutInfo.cutNormal.x + " " + cutInfo.cutNormal.y + " " + cutInfo.cutNormal.z + " " +
				//	"\ncutDirDeviation:" + cutInfo.cutDirDeviation +
				//	"\ntimeDeviation:" + cutInfo.timeDeviation +
				//	"\nsaberSpeed:" + cutInfo.saberSpeed +
				//	"\nwasCutTooSoon:" + cutInfo.wasCutTooSoon +
				//	"\ncutPoint:" + cutInfo.cutPoint.x + " " + cutInfo.cutPoint.y + " " + cutInfo.cutPoint.z + " " +
				//	"\nsaberDir:" + cutInfo.saberDir.x + " " + cutInfo.saberDir.y + " " + cutInfo.saberDir.z + " " +
					"\nconfig:" + PluginConfig.LeftHandSaberDirection.x + " " + PluginConfig.LeftHandSaberDirection.y + " " + PluginConfig.LeftHandSaberDirection.z + " \n\n";

		}
	}
}
