﻿using Discord.API.Rest;
using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Model = Discord.API.Channel;

namespace Discord.WebSocket
{
    [DebuggerDisplay(@"{DebuggerDisplay,nq}")]
    public class SocketTextChannel : SocketGuildChannel, ITextChannel, ISocketMessageChannel
    {
        private readonly MessageCache _messages;

        public string Topic { get; private set; }

        public string Mention => MentionUtils.MentionChannel(Id);
        public IReadOnlyCollection<SocketMessage> CachedMessages => _messages?.Messages ?? ImmutableArray.Create<SocketMessage>();
        public override IReadOnlyCollection<SocketGuildUser> Users
            => Guild.Users.Where(x => Permissions.GetValue(
                Permissions.ResolveChannel(Guild, x, this, Permissions.ResolveGuild(Guild, x)), 
                ChannelPermission.ReadMessages)).ToImmutableArray();
        
        internal SocketTextChannel(DiscordSocketClient discord, ulong id, SocketGuild guild)
            : base(discord, id, guild)
        {
            if (Discord.MessageCacheSize > 0)
                _messages = new MessageCache(Discord, this);
        }
        internal new static SocketTextChannel Create(SocketGuild guild, ClientState state, Model model)
        {
            var entity = new SocketTextChannel(guild.Discord, model.Id, guild);
            entity.Update(state, model);
            return entity;
        }
        internal override void Update(ClientState state, Model model)
        {
            base.Update(state, model);

            Topic = model.Topic.Value;
        }

        public Task ModifyAsync(Action<ModifyTextChannelParams> func)
            => ChannelHelper.ModifyAsync(this, Discord, func);

        //Messages
        public SocketMessage GetCachedMessage(ulong id)
            => _messages?.Get(id);
        public async Task<IMessage> GetMessageAsync(ulong id)
        {
            IMessage msg = _messages?.Get(id);
            if (msg == null)
                msg = await ChannelHelper.GetMessageAsync(this, Discord, id);
            return msg;
        }
        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(int limit = DiscordConfig.MaxMessagesPerBatch)
            => SocketChannelHelper.GetMessagesAsync(this, Discord, _messages, null, Direction.Before, limit, CacheMode.AllowDownload);
        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(ulong fromMessageId, Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
            => SocketChannelHelper.GetMessagesAsync(this, Discord, _messages, fromMessageId, dir, limit, CacheMode.AllowDownload);
        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(IMessage fromMessage, Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
            => SocketChannelHelper.GetMessagesAsync(this, Discord, _messages, fromMessage.Id, dir, limit, CacheMode.AllowDownload);
        public IReadOnlyCollection<SocketMessage> GetCachedMessages(int limit = DiscordConfig.MaxMessagesPerBatch)
            => SocketChannelHelper.GetCachedMessages(this, Discord, _messages, null, Direction.Before, limit);
        public IReadOnlyCollection<SocketMessage> GetCachedMessages(ulong fromMessageId, Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
            => SocketChannelHelper.GetCachedMessages(this, Discord, _messages, fromMessageId, dir, limit);
        public IReadOnlyCollection<SocketMessage> GetCachedMessages(IMessage fromMessage, Direction dir, int limit = DiscordConfig.MaxMessagesPerBatch)
            => SocketChannelHelper.GetCachedMessages(this, Discord, _messages, fromMessage.Id, dir, limit);
        public Task<IReadOnlyCollection<RestMessage>> GetPinnedMessagesAsync()
            => ChannelHelper.GetPinnedMessagesAsync(this, Discord);

        public Task<RestUserMessage> SendMessageAsync(string text, bool isTTS)
            => ChannelHelper.SendMessageAsync(this, Discord, text, isTTS);
        public Task<RestUserMessage> SendFileAsync(string filePath, string text, bool isTTS)
            => ChannelHelper.SendFileAsync(this, Discord, filePath, text, isTTS);
        public Task<RestUserMessage> SendFileAsync(Stream stream, string filename, string text, bool isTTS)
            => ChannelHelper.SendFileAsync(this, Discord, stream, filename, text, isTTS);

        public Task DeleteMessagesAsync(IEnumerable<IMessage> messages)
            => ChannelHelper.DeleteMessagesAsync(this, Discord, messages);

        public IDisposable EnterTypingState()
            => ChannelHelper.EnterTypingState(this, Discord);

        internal void AddMessage(SocketMessage msg)
            => _messages.Add(msg);
        internal SocketMessage RemoveMessage(ulong id)
            => _messages.Remove(id);

        //Users
        public override SocketGuildUser GetUser(ulong id)
        {
            var user = Guild.GetUser(id);
            if (user != null)
            {
                var guildPerms = Permissions.ResolveGuild(Guild, user);
                var channelPerms = Permissions.ResolveChannel(Guild, user, this, guildPerms);
                if (Permissions.GetValue(channelPerms, ChannelPermission.ReadMessages))
                    return user;
            }
            return null;
        }
        
        private string DebuggerDisplay => $"{Name} ({Id}, Text)";
        internal new SocketTextChannel Clone() => MemberwiseClone() as SocketTextChannel;

        //IGuildChannel
        Task<IGuildUser> IGuildChannel.GetUserAsync(ulong id, CacheMode mode)
            => Task.FromResult<IGuildUser>(GetUser(id));
        IAsyncEnumerable<IReadOnlyCollection<IGuildUser>> IGuildChannel.GetUsersAsync(CacheMode mode)
            => ImmutableArray.Create<IReadOnlyCollection<IGuildUser>>(Users).ToAsyncEnumerable();

        //IMessageChannel
        async Task<IMessage> IMessageChannel.GetMessageAsync(ulong id, CacheMode mode)
        {
            if (mode == CacheMode.AllowDownload)
                return await GetMessageAsync(id);
            else
                return GetCachedMessage(id);
        }
        IAsyncEnumerable<IReadOnlyCollection<IMessage>> IMessageChannel.GetMessagesAsync(int limit, CacheMode mode)
            => SocketChannelHelper.GetMessagesAsync(this, Discord, _messages, null, Direction.Before, limit, mode);
        IAsyncEnumerable<IReadOnlyCollection<IMessage>> IMessageChannel.GetMessagesAsync(ulong fromMessageId, Direction dir, int limit, CacheMode mode)
            => SocketChannelHelper.GetMessagesAsync(this, Discord, _messages, fromMessageId, dir, limit, mode);
        IAsyncEnumerable<IReadOnlyCollection<IMessage>> IMessageChannel.GetMessagesAsync(IMessage fromMessage, Direction dir, int limit, CacheMode mode)
            => SocketChannelHelper.GetMessagesAsync(this, Discord, _messages, fromMessage.Id, dir, limit, mode);
        async Task<IReadOnlyCollection<IMessage>> IMessageChannel.GetPinnedMessagesAsync()
            => await GetPinnedMessagesAsync().ConfigureAwait(false);
        async Task<IUserMessage> IMessageChannel.SendFileAsync(string filePath, string text, bool isTTS)
            => await SendFileAsync(filePath, text, isTTS);
        async Task<IUserMessage> IMessageChannel.SendFileAsync(Stream stream, string filename, string text, bool isTTS)
            => await SendFileAsync(stream, filename, text, isTTS);
        async Task<IUserMessage> IMessageChannel.SendMessageAsync(string text, bool isTTS)
            => await SendMessageAsync(text, isTTS);
        IDisposable IMessageChannel.EnterTypingState()
            => EnterTypingState();
    }
}