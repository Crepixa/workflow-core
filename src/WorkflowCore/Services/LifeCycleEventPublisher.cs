﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models.LifeCycleEvents;

namespace WorkflowCore.Services
{
    public class LifeCycleEventPublisher : ILifeCycleEventPublisher, IDisposable
    {
        private readonly ILifeCycleEventHub _eventHub;
        private readonly ILogger _logger;
        private readonly BlockingCollection<LifeCycleEvent> _outbox;
        protected Task DispatchTask;

        public LifeCycleEventPublisher(ILifeCycleEventHub eventHub, ILoggerFactory loggerFactory)
        {
            _eventHub = eventHub;
            _outbox = new BlockingCollection<LifeCycleEvent>();
            _logger = loggerFactory.CreateLogger(GetType());
        }

        public void PublishNotification(LifeCycleEvent evt)
        {
            if (_outbox.IsAddingCompleted)
                return;

            _outbox.Add(evt);
        }

        public void Start()
        {
            if (DispatchTask != null)
            {
                throw new InvalidOperationException();
            }

            DispatchTask = new Task(Execute);
            DispatchTask.Start();
        }

        public void Stop()
        {
            _outbox.CompleteAdding();

            DispatchTask.Wait();
            DispatchTask = null;
        }

        public void Dispose()
        {
            _outbox.Dispose();
        }

        private async void Execute()
        {
            try
            {
                foreach (var evt in _outbox.GetConsumingEnumerable())
                {
                    await _eventHub.PublishNotification(evt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(default(EventId), ex, ex.Message);
            }
        }
    }
}