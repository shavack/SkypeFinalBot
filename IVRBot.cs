using System.Runtime.InteropServices;

namespace EmergencyServicesBot
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Calling;
    using Microsoft.Bot.Builder.Calling.Events;
    using Microsoft.Bot.Builder.Calling.ObjectModel.Contracts;
    using Microsoft.Bot.Builder.Calling.ObjectModel.Misc;

    public class IVRBot : IDisposable, ICallingBot
    {
        private readonly MicrosoftCognitiveSpeechService speechService = new MicrosoftCognitiveSpeechService();
        private int i;
        private IncomingCallEvent incomingCallEvent;

        public enum Intent
        {
            WelcomeMessage = 0,
            DocQuery = 1,
            CarQuery = 2,
            CarSpecification = 3,
            DocSpecification = 4,
        }

        public IVRBot(ICallingBotService callingBotService)
        {
            this.CallingBotService = callingBotService;

            this.CallingBotService.OnIncomingCallReceived += this.OnIncomingCallReceived;
            this.CallingBotService.OnRecordCompleted += this.OnRecordCompleted;
            this.CallingBotService.OnHangupCompleted += OnHangupCompleted;
        }

        public ICallingBotService CallingBotService { get; }

        public void Dispose()
        {
            if (this.CallingBotService != null)
            {
                this.CallingBotService.OnIncomingCallReceived -= this.OnIncomingCallReceived;
                this.CallingBotService.OnRecordCompleted -= this.OnRecordCompleted;
                this.CallingBotService.OnHangupCompleted -= OnHangupCompleted;
            }
        }

        private static Task OnHangupCompleted(HangupOutcomeEvent hangupOutcomeEvent)
        {
            hangupOutcomeEvent.ResultingWorkflow = null;
            return Task.FromResult(true);
        }

        private Task OnIncomingCallReceived(IncomingCallEvent incomingCallEvent)
        {

            var record = CreateRecordingAction("Hello! It's mega ultra super B A D Sparrows bot! How may I help you? You can choose documents or car.", Intent.WelcomeMessage);

            this.incomingCallEvent = incomingCallEvent;
            this.incomingCallEvent.ResultingWorkflow.Actions = new List<ActionBase> {
                new Answer { OperationId = Guid.NewGuid().ToString()},
                record,
            };


            return Task.FromResult(true);
        }

        private ActionBase CreateRecordingAction(string text, Intent intent)
        {
            return new Record
            {
                OperationId = ((int)intent).ToString(),
                PlayPrompt = new PlayPrompt { OperationId = Guid.NewGuid().ToString(), Prompts = new List<Prompt> { new Prompt { Value = text } } },
                RecordingFormat = RecordingFormat.Wav,
                MaxDurationInSeconds = 2,
                PlayBeep = false,
            };
        }

        private ActionBase CreatePlayPromptAction(string text)
        {
            return new PlayPrompt
            {
                OperationId = Guid.NewGuid().ToString(),
                Prompts = new List<Prompt> { new Prompt { Value = text } }
            };
        }

        private async Task OnRecordCompleted(RecordOutcomeEvent recordOutcomeEvent)
        {
            var actions = new List<ActionBase>();

            var spokenText = string.Empty;

            try
            {
                if (recordOutcomeEvent.RecordOutcome.Outcome == Outcome.Success)
                {
                    var record = await recordOutcomeEvent.RecordedContent;
                    spokenText = await this.speechService.GetTextFromAudioAsync(record);

                    switch (int.Parse(recordOutcomeEvent.RecordOutcome.Id))
                    {
                        case (int)Intent.WelcomeMessage:
                            if (spokenText.ToUpperInvariant().Contains("CAR"))
                            {
                                actions.Add(CreateRecordingAction("what kind of car do you want? ", Intent.CarQuery));
                            }
                            else if (spokenText.ToUpperInvariant().Contains("DOCUMENT") &&
                                     !spokenText.ToUpperInvariant().Contains("CAR"))

                            {
                                actions.Add(CreateRecordingAction("what kind of docs do you want to send?", Intent.DocQuery));
                            }
                            else
                            {
                                if (spokenText.Contains("REPEAT"))
                                {
                                    actions.Add(CreateRecordingAction(
                                        $"We couldn't recognize your message. You should choose either Documents or Car. Please repeat.", Intent.WelcomeMessage));
                                }
                                else
                                {
                                    actions.Add(CreateRecordingAction(
                                        $"We couldn't recognize your message. Did you say {spokenText}? You should choose either Documents or Car. Please repeat.", Intent.WelcomeMessage));
                                }
                                
                            }
                            break;
                        case (int)Intent.CarQuery:
                            if (spokenText.ToUpperInvariant().Contains("MUSTANG"))
                            {
                                actions.Add(CreatePlayPromptAction("Your mustang will arrive soon. Thank you for using our service."));
                                actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });
                            }
                            else if (spokenText.ToUpperInvariant().Contains("FERRARI"))
                            {
                                actions.Add(CreatePlayPromptAction("Your Ferrari will arrive soon. Thank you for using our service."));
                                actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });
                            }
                            else
                            {
                                actions.Add(CreatePlayPromptAction(
                                    "We weren't able to understand your request (you didn't choose Mustang or Ferrari), so you will get a fiat multipla. see ya!"));
                                actions.Add(new Hangup {OperationId = Guid.NewGuid().ToString()});
                            }
                            break;
                        case (int)Intent.DocQuery:
                            if (spokenText.ToUpperInvariant().Contains("CREDIT CARD"))
                            {
                                actions.Add(CreatePlayPromptAction("Your credit card info will be sent soon. Thank you for using our service."));
                                actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });
                            }
                            else if (spokenText.ToUpperInvariant().Contains("DRIVING LICENCE"))
                            {
                                actions.Add(CreatePlayPromptAction("Your driving licence info will be sent soon. Thank you for using our service."));
                                actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });
                            }
                            else
                            {
                                actions.Add(CreatePlayPromptAction("We weren't able to understand your request (you didn't choose Credit card or Driving licence, so you will get some spam. See ya!"));
                                actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });
                            }
                            break;
                        case (int)Intent.CarSpecification:
                            break;
                        case (int)Intent.DocSpecification:
                            break;
                    }
                }
            else
            {
                actions.Add(CreatePlayPromptAction("Sorry, there was an issue."));
            }

            //actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() }); // hang up the call
            recordOutcomeEvent.ResultingWorkflow.Actions = actions;
            this.incomingCallEvent.ResultingWorkflow.Actions = actions;
            }
            catch (Exception e)
            {
                var action = CreatePlayPromptAction(e.ToString());
                actions.Add(action);
                actions.Add(new Hangup { OperationId = Guid.NewGuid().ToString() });
                recordOutcomeEvent.ResultingWorkflow.Actions = actions;
                this.incomingCallEvent.ResultingWorkflow.Actions = actions;
            }
        }
    }
}