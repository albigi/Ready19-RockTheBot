﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Ready19.RockTheBot
{
    /// <summary>
    /// Stores User Welcome state for the conversation.
    /// Stored in <see cref="Microsoft.Bot.Builder.ConversationState"/> and
    /// backed by <see cref="Microsoft.Bot.Builder.MemoryStorage"/>.
    /// </summary>
    public class WelcomeUserState
    {
        /// <summary>
        /// Gets or sets a value indicating whether the user has been welcomed in the conversation.
        /// </summary>
        /// <value>The user has been welcomed in the conversation.</value>
        public bool DidBotWelcomeUser { get; set; } = false;
    }
}
