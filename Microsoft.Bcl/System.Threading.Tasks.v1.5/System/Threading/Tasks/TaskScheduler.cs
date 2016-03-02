// ==++==
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// TaskScheduler.cs
//
// <OWNER>hyildiz</OWNER>
//
// This file contains the primary interface and management of tasks and queues.  
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Security;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks
{
 
    /// <summary>
    /// Represents an abstract scheduler for tasks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see> acts as the extension point for all 
    /// pluggable scheduling logic.  This includes mechanisms such as how to schedule a task for execution, and
    /// how scheduled tasks should be exposed to debuggers.
    /// </para>
    /// <para>
    /// All members of the abstract <see cref="TaskScheduler"/> type are thread-safe
    /// and may be used from multiple threads concurrently.
    /// </para>
    /// </remarks>
    [DebuggerDisplay("Id={Id}")]
    [DebuggerTypeProxy(typeof(SystemThreadingTasks_TaskSchedulerDebugView))]
    
        
    public abstract class TaskScheduler
    {
        ////////////////////////////////////////////////////////////
        //
        // User Provided Methods and Properties
        //
        
        /// <summary>
        /// Queues a <see cref="T:System.Threading.Tasks.Task">Task</see> to the scheduler.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A class derived from <see cref="T:System.Threading.Tasks.TaskScheduler">TaskScheduler</see>  
        /// implements this method to accept tasks being scheduled on the scheduler.
        /// A typical implementation would store the task in an internal data structure, which would
        /// be serviced by threads that would execute those tasks at some time in the future.
        /// </para>
        /// <para>
        /// This method is only meant to be called by the .NET Framework and
        /// should not be called directly by the derived class. This is necessary 
        /// for maintaining the consistency of the system.
        /// </para>
        /// </remarks>
        /// <param name="task">The <see cref="T:System.Threading.Tasks.Task">Task</see> to be queued.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="task"/> argument is null.</exception>
        // [SecurityCritical]
        protected internal abstract void QueueTask(Task task);

        /// <summary>
        /// Determines whether the provided <see cref="T:System.Threading.Tasks.Task">Task</see>
        /// can be executed synchronously in this call, and if it can, executes it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A class derived from <see cref="TaskScheduler">TaskScheduler</see> implements this function to
        /// support inline execution of a task on a thread that initiates a wait on that task object. Inline
        /// execution is optional, and the request may be rejected by returning false. However, better
        /// scalability typically results the more tasks that can be inlined, and in fact a scheduler that
        /// inlines too little may be prone to deadlocks. A proper implementation should ensure that a
        /// request executing under the policies guaranteed by the scheduler can successfully inline. For
        /// example, if a scheduler uses a dedicated thread to execute tasks, any inlining requests from that
        /// thread should succeed.
        /// </para>
        /// <para>
        /// If a scheduler decides to perform the inline execution, it should do so by calling to the base
        /// TaskScheduler's
        /// <see cref="TryExecuteTask">TryExecuteTask</see> method with the provided task object, propagating
        /// the return value. It may also be appropriate for the scheduler to remove an inlined task from its
        /// internal data structures if it decides to honor the inlining request. Note, however, that under
        /// some circumstances a scheduler may be asked to inline a task that was not previously provided to
        /// it with the <see cref="QueueTask"/> method.
        /// </para>
        /// <para>
        /// The derived scheduler is responsible for making sure that the calling thread is suitable for
        /// executing the given task as far as its own scheduling and execution policies are concerned.
        /// </para>
        /// </remarks>
        /// <param name="task">The <see cref="T:System.Threading.Tasks.Task">Task</see> to be
        /// executed.</param>
        /// <param name="taskWasPreviouslyQueued">A Boolean denoting whether or not task has previously been
        /// queued. If this parameter is True, then the task may have been previously queued (scheduled); if
        /// False, then the task is known not to have been queued, and this call is being made in order to
        /// execute the task inline without queueing it.</param>
        /// <returns>A Boolean value indicating whether the task was executed inline.</returns>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="task"/> argument is
        /// null.</exception>
        /// <exception cref="T:System.InvalidOperationException">The <paramref name="task"/> was already
        /// executed.</exception>
        // [SecurityCritical]
        protected abstract bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued);

        /// <summary>
        /// Generates an enumerable of <see cref="T:System.Threading.Tasks.Task">Task</see> instances
        /// currently queued to the scheduler waiting to be executed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A class derived from <see cref="TaskScheduler"/> implements this method in order to support
        /// integration with debuggers. This method will only be invoked by the .NET Framework when the
        /// debugger requests access to the data. The enumerable returned will be traversed by debugging
        /// utilities to access the tasks currently queued to this scheduler, enabling the debugger to
        /// provide a representation of this information in the user interface.
        /// </para>
        /// <para>
        /// It is important to note that, when this method is called, all other threads in the process will
        /// be frozen. Therefore, it's important to avoid synchronization with other threads that may lead to
        /// blocking. If synchronization is necessary, the method should prefer to throw a <see
        /// cref="System.NotSupportedException"/>
        /// than to block, which could cause a debugger to experience delays. Additionally, this method and
        /// the enumerable returned must not modify any globally visible state.
        /// </para>
        /// <para>
        /// The returned enumerable should never be null. If there are currently no queued tasks, an empty
        /// enumerable should be returned instead.
        /// </para>
        /// <para>
        /// For developers implementing a custom debugger, this method shouldn't be called directly, but
        /// rather this functionality should be accessed through the internal wrapper method
        /// GetScheduledTasksForDebugger:
        /// <c>internal Task[] GetScheduledTasksForDebugger()</c>. This method returns an array of tasks,
        /// rather than an enumerable. In order to retrieve a list of active schedulers, a debugger may use
        /// another internal method: <c>internal static TaskScheduler[] GetTaskSchedulersForDebugger()</c>.
        /// This static method returns an array of all active TaskScheduler instances.
        /// GetScheduledTasksForDebugger then may be used on each of these scheduler instances to retrieve
        /// the list of scheduled tasks for each.
        /// </para>
        /// </remarks>
        /// <returns>An enumerable that allows traversal of tasks currently queued to this scheduler.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">
        /// This scheduler is unable to generate a list of queued tasks at this time.
        /// </exception>
        // [SecurityCritical]
        protected abstract IEnumerable<Task> GetScheduledTasks();

        /// <summary>
        /// Indicates the maximum concurrency level this 
        /// <see cref="TaskScheduler"/>  is able to support.
        /// </summary>
        public virtual Int32 MaximumConcurrencyLevel
        {
            get
            {
                return Int32.MaxValue;
            }
        }


        ////////////////////////////////////////////////////////////
        //
        // Internal overridable methods
        //
        
        /// <summary>
        /// Retrieves some thread static state that can be cached and passed to multiple
        /// TryRunInline calls, avoiding superflous TLS fetches.
        /// </summary>
        /// <returns>A bag of TLS state (or null if none exists).</returns>
        internal virtual object GetThreadStatics()
        {
            return null;
        }

        /// <summary>
        /// Attempts to execute the target task synchronously.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="taskWasPreviouslyQueued">True if the task may have been previously queued,
        /// false if the task was absolutely not previously queued.</param>
        /// <param name="threadStatics">The state retrieved from GetThreadStatics</param>
        /// <returns>True if it ran, false otherwise.</returns>
        // [SecuritySafeCritical]
        internal bool TryRunInline(Task task, bool taskWasPreviouslyQueued, object threadStatics)
        {
            // Do not inline unstarted tasks (i.e., task.ExecutingTaskScheduler == null).
            // Do not inline TaskCompletionSource-style (a.k.a. "promise") tasks.
            // No need to attempt inlining if the task body was already run (i.e. either TASK_STATE_DELEGATE_INVOKED or TASK_STATE_CANCELED bits set)
            TaskScheduler ets = task.ExecutingTaskScheduler;
            
            // Delegate cross-scheduler inlining requests to target scheduler
            if(ets != this && ets !=null) return ets.TryRunInline(task, taskWasPreviouslyQueued);
                        
            if( (ets == null) ||
                (task.m_action == null) ||
                task.IsDelegateInvoked || 
                task.IsCanceled)
            {
                return false;
            }

            // Task class will still call into TaskScheduler.TryRunInline rather than TryExecuteTaskInline() so that 
            // 1) we can adjust the return code from TryExecuteTaskInline in case a buggy custom scheduler lies to us
            // 2) we maintain a mechanism for the TLS lookup optimization that we used to have for the ConcRT scheduler (will potentially introduce the same for TP)
            bool bInlined = TryExecuteTaskInline(task, taskWasPreviouslyQueued);

            // If the custom scheduler returned true, we should either have the TASK_STATE_DELEGATE_INVOKED or TASK_STATE_CANCELED bit set
            // Otherwise the scheduler is buggy
            if (bInlined && !(task.IsDelegateInvoked || task.IsCanceled)) 
            {
                throw new InvalidOperationException(Strings.TaskScheduler_InconsistentStateAfterTryExecuteTaskInline);
            }

            return bInlined;
        }

        // [SecuritySafeCritical]
        internal bool TryRunInline(Task task, bool taskWasPreviouslyQueued)
        {
            return TryRunInline(task, taskWasPreviouslyQueued, GetThreadStatics());
        }

        /// <summary>
        /// Attempts to dequeue a <see cref="T:System.Threading.Tasks.Task">Task</see> that was previously queued to
        /// this scheduler.
        /// </summary>
        /// <param name="task">The <see cref="T:System.Threading.Tasks.Task">Task</see> to be dequeued.</param>
        /// <returns>A Boolean denoting whether the <paramref name="task"/> argument was successfully dequeued.</returns>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="task"/> argument is null.</exception>
        // [SecurityCritical]
        protected internal virtual bool TryDequeue(Task task)
        {
            return false;
        }

        /// <summary>
        /// Notifies the scheduler that a work item has made progress.
        /// </summary>
        internal virtual void NotifyWorkItemProgress()
        { 
        }

        /// <summary>
        /// Indicates whether this is a custom scheduler, in which case the safe code paths will be taken upon task entry
        /// using a CAS to transition from queued state to executing.
        /// </summary>
        internal virtual bool RequiresAtomicStartTransition
        {
            get { return true; }
        }



        ////////////////////////////////////////////////////////////
        //
        // Member variables
        //
        
        // An AppDomain-wide default manager.
        private static TaskScheduler s_defaultTaskScheduler = new ThreadPoolTaskScheduler();

        //static counter used to generate unique TaskScheduler IDs
        internal static int s_taskSchedulerIdCounter;

        // this TaskScheduler's unique ID
        private int m_taskSchedulerId;

        // We keep a weak reference to ourselves to be uniquely identified in the global 
        // static collection of active schedulers without being pinned down in memory, as well 
        // to convert it later to a real TS object when enumerating for the debugger
        internal WeakReference m_weakReferenceToSelf;

        // The global container that keeps track of TaskScheduler instances. Lazily initialized
        private static ConcurrentDictionary<WeakReference, object> s_activeTaskSchedulers;


        ////////////////////////////////////////////////////////////
        //
        // Constructors and public properties
        //
        
        /// <summary>
        /// Initializes the <see cref="System.Threading.Tasks.TaskScheduler"/>.
        /// </summary>
        protected TaskScheduler()
        {
            // Protected constructor. It's here to ensure all user implemented TaskSchedulers will be 
            // registered in the active schedulers list.
            m_weakReferenceToSelf = new WeakReference(this);
            RegisterTaskScheduler(this);
        }

        /// <summary>
        /// Frees all resources associated with this scheduler.
        /// </summary>
        ~TaskScheduler()
        {
            // Finalizer to remove us out of the active schedulers list
            UnregisterTaskScheduler(this);
        }

        /// <summary>
        /// Gets the default <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see> instance.
        /// </summary>
        public static TaskScheduler Default 
        {
            get
            {
                return s_defaultTaskScheduler;
            }
        }

        /// <summary>
        /// Gets the <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
        /// associated with the currently executing task.
        /// </summary>
        /// <remarks>
        /// When not called from within a task, <see cref="Current"/> will return the <see cref="Default"/> scheduler.
        /// </remarks>
        public static TaskScheduler Current 
        {
            get
            {
                Task currentTask = Task.InternalCurrent;

                if (currentTask != null)
                {
                    return currentTask.ExecutingTaskScheduler;
                }
                else
                {
                    return TaskScheduler.Default;
                }
            }
        }

        /// <summary>
        /// Creates a <see cref="TaskScheduler"/>
        /// associated with the current <see cref="T:System.Threading.SynchronizationContext"/>.
        /// </summary>
        /// <remarks>
        /// All <see cref="System.Threading.Tasks.Task">Task</see> instances queued to 
        /// the returned scheduler will be executed through a call to the
        /// <see cref="System.Threading.SynchronizationContext.Post">Post</see> method
        /// on that context.
        /// </remarks>
        /// <returns>
        /// A <see cref="TaskScheduler"/> associated with 
        /// the current <see cref="T:System.Threading.SynchronizationContext">SynchronizationContext</see>, as
        /// determined by <see cref="System.Threading.SynchronizationContext.Current">SynchronizationContext.Current</see>.
        /// </returns>
        /// <exception cref="T:System.InvalidOperationException">
        /// The current SynchronizationContext may not be used as a TaskScheduler.
        /// </exception>
        public static TaskScheduler FromCurrentSynchronizationContext()
        {
            return new SynchronizationContextTaskScheduler();
        }

        /// <summary>
        /// Gets the unique ID for this <see cref="TaskScheduler"/>.
        /// </summary>
        public Int32 Id
        {
            get
            {
                if (m_taskSchedulerId == 0)
                {
                    int newId = 0;

                    // We need to repeat if Interlocked.Increment wraps around and returns 0.
                    // Otherwise next time this scheduler's Id is queried it will get a new value
                    do
                    {
                        newId = Interlocked.Increment(ref s_taskSchedulerIdCounter);
                    } while (newId == 0);
                    
                    Interlocked.CompareExchange(ref m_taskSchedulerId, newId, 0);
                }

                return m_taskSchedulerId;
            }
        }

        /// <summary>
        /// Attempts to execute the provided <see cref="T:System.Threading.Tasks.Task">Task</see>
        /// on this scheduler.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Scheduler implementations are provided with <see cref="T:System.Threading.Tasks.Task">Task</see>
        /// instances to be executed through either the <see cref="QueueTask"/> method or the
        /// <see cref="TryExecuteTaskInline"/> method. When the scheduler deems it appropriate to run the
        /// provided task, <see cref="TryExecuteTask"/> should be used to do so. TryExecuteTask handles all
        /// aspects of executing a task, including action invocation, exception handling, state management,
        /// and lifecycle control.
        /// </para>
        /// <para>
        /// <see cref="TryExecuteTask"/> must only be used for tasks provided to this scheduler by the .NET
        /// Framework infrastructure. It should not be used to execute arbitrary tasks obtained through
        /// custom mechanisms.
        /// </para>
        /// </remarks>
        /// <param name="task">
        /// A <see cref="T:System.Threading.Tasks.Task">Task</see> object to be executed.</param>
        /// <exception cref="T:System.InvalidOperationException">
        /// The <paramref name="task"/> is not associated with this scheduler.
        /// </exception>
        /// <returns>A Boolean that is true if <paramref name="task"/> was successfully executed, false if it
        /// was not. A common reason for execution failure is that the task had previously been executed or
        /// is in the process of being executed by another thread.</returns>
        // [SecurityCritical]
        protected bool TryExecuteTask(Task task)
        {
            if (task.ExecutingTaskScheduler != this)
            {
                throw new InvalidOperationException(Strings.TaskScheduler_ExecuteTask_WrongTaskScheduler);
            }

            return task.ExecuteEntry(true);
        }

        ////////////////////////////////////////////////////////////
        //
        // Events
        //

        private static event EventHandler<UnobservedTaskExceptionEventArgs> _unobservedTaskException;
        private static object _unobservedTaskExceptionLockObject = new object();

        /// <summary>
        /// Occurs when a faulted <see cref="System.Threading.Tasks.Task"/>'s unobserved exception is about to trigger exception escalation
        /// policy, which, by default, would terminate the process.
        /// </summary>
        /// <remarks>
        /// This AppDomain-wide event provides a mechanism to prevent exception
        /// escalation policy (which, by default, terminates the process) from triggering. 
        /// Each handler is passed a <see cref="T:System.Threading.Tasks.UnobservedTaskExceptionEventArgs"/>
        /// instance, which may be used to examine the exception and to mark it as observed.
        /// </remarks>
        public static event EventHandler<UnobservedTaskExceptionEventArgs> UnobservedTaskException
        {
            // [System.Security.SecurityCritical]
            add
            {
                if (value != null)
                {
//#if !PFX_LEGACY_3_5
//                    RuntimeHelpers.PrepareContractedDelegate(value);
//#endif
                    lock (_unobservedTaskExceptionLockObject) _unobservedTaskException += value;
                }
            }

            // [System.Security.SecurityCritical]
            remove
            {
                lock (_unobservedTaskExceptionLockObject) _unobservedTaskException -= value;
            }
        }
                    




        
        ////////////////////////////////////////////////////////////
        //
        // Internal methods
        //

        // This is called by the TaskExceptionHolder finalizer.
        internal static void PublishUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs ueea)
        {
            // Lock this logic to prevent just-unregistered handlers from being called.
            lock (_unobservedTaskExceptionLockObject)
            {
                // Since we are under lock, it is technically no longer necessary
                // to make a copy.  It is done here for convenience.
                EventHandler<UnobservedTaskExceptionEventArgs> handler = _unobservedTaskException;
                if (handler != null)
                {
                    handler(sender, ueea);
                }
            }
        }

        /// <summary>
        /// Provides an array of all queued <see cref="System.Threading.Tasks.Task">Task</see> instances
        /// for the debugger.
        /// </summary>
        /// <remarks>
        /// The returned array is populated through a call to <see cref="GetScheduledTasks"/>.
        /// Note that this function is only meant to be invoked by a debugger remotely. 
        /// It should not be called by any other codepaths.
        /// </remarks>
        /// <returns>An array of <see cref="System.Threading.Tasks.Task">Task</see> instances.</returns> 
        /// <exception cref="T:System.NotSupportedException">
        /// This scheduler is unable to generate a list of queued tasks at this time.
        /// </exception>
        internal Task[] GetScheduledTasksForDebugger()
        {
            // this can throw InvalidOperationException indicating that they are unable to provide the info
            // at the moment. We should let the debugger receive that exception so that it can indicate it in the UI
            IEnumerable<Task> activeTasksSource = GetScheduledTasks();

            if (activeTasksSource == null)
                return null;

            // If it can be cast to an array, use it directly
            Task[] activeTasksArray = activeTasksSource as Task[];
            if (activeTasksArray == null)
            {
                activeTasksArray = (new List<Task>(activeTasksSource)).ToArray();
            }

            // touch all Task.Id fields so that the debugger doesn't need to do a lot of cross-proc calls to generate them
            foreach (Task t in activeTasksArray)
            {
                int tmp = t.Id;
            }

            return activeTasksArray;
        }

        /// <summary>
        /// Provides an array of all active <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see> 
        /// instances for the debugger.
        /// </summary>
        /// <remarks>
        /// This function is only meant to be invoked by a debugger remotely. 
        /// It should not be called by any other codepaths.
        /// </remarks>
        /// <returns>An array of <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see> instances.</returns> 
        internal static TaskScheduler[] GetTaskSchedulersForDebugger()
        {
            // To populate this array we walk the global collection of schedulers (s_activeTaskSchedulers).

            TaskScheduler[] activeSchedulers = new TaskScheduler[s_activeTaskSchedulers.Count];

            IEnumerator<KeyValuePair<WeakReference,object>> tsEnumerator = s_activeTaskSchedulers.GetEnumerator();

            int index = 0;
            while (tsEnumerator.MoveNext())
            {
                // convert the weak reference to the real TS object
                TaskScheduler ts = tsEnumerator.Current.Key.Target as TaskScheduler;
                if (ts == null)
                    continue;
                activeSchedulers[index++] = ts;                
                int tmp = ts.Id;
            }
            
            return activeSchedulers;
        }

        /// <summary>
        /// Registers a new TaskScheduler instance in the global collection of schedulers.
        /// </summary>
        internal static void RegisterTaskScheduler(TaskScheduler ts)
        {
            LazyInitializer.EnsureInitialized<ConcurrentDictionary<WeakReference, object>>(ref s_activeTaskSchedulers);

            bool bResult = s_activeTaskSchedulers.TryAdd(ts.m_weakReferenceToSelf, null);
            Contract.Assert(bResult);
        }

        /// <summary>
        /// Removes a TaskScheduler instance from the global collection of schedulers.
        /// </summary>
        internal static void UnregisterTaskScheduler(TaskScheduler ts)
        {
            Contract.Assert(s_activeTaskSchedulers != null);

            object tmpObj;
            bool bResult = s_activeTaskSchedulers.TryRemove(ts.m_weakReferenceToSelf, out tmpObj);

            Contract.Assert(bResult);
        }


        /// <summary>
        /// Nested class that provides debugger view for TaskScheduler
        /// </summary>
        internal sealed class SystemThreadingTasks_TaskSchedulerDebugView
        {
            private readonly TaskScheduler m_taskScheduler;
            public SystemThreadingTasks_TaskSchedulerDebugView(TaskScheduler scheduler)
            {
                m_taskScheduler = scheduler;
            }

            // returns the schedulerís Id
            public Int32 Id
            { 
                get { return m_taskScheduler.Id; } 
            }

            // returns the schedulerís GetScheduledTasks
            public IEnumerable<Task> ScheduledTasks 
            {
                // [SecurityCritical]
                get { return m_taskScheduler.GetScheduledTasks(); }
            }
        }
    }

    /// <summary>Default thread pool scheduler.</summary>
    internal sealed class ThreadPoolTaskScheduler : TaskScheduler
    {
        private WaitCallback m_wcCallback;
        private Action<object> m_ptsCallback;

        internal ThreadPoolTaskScheduler()
        {
            m_wcCallback = state => TryExecuteTask((Task)state);
            m_ptsCallback = state => TryExecuteTask((Task)state);
        }

        protected internal override void QueueTask(Task task)
        {
            if ((task.CreationOptions & TaskCreationOptions.LongRunning) != 0)
            {
                ThreadLightup thread = ThreadLightup.Create(m_ptsCallback);
                thread.IsBackground = true;
                thread.Start(task);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(m_wcCallback, task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return TryExecuteTask(task);
        }

        protected override IEnumerable<Task> GetScheduledTasks() { return null; }
    }

    /// <summary>
    /// A TaskScheduler implementation that executes all tasks queued to it through a call to 
    /// <see cref="System.Threading.SynchronizationContext.Post"/> on the <see cref="T:System.Threading.SynchronizationContext"/> 
    /// that its associated with. The default constructor for this class binds to the current <see cref="T:System.Threading.SynchronizationContext"/> 
    /// </summary>
    internal sealed class SynchronizationContextTaskScheduler : TaskScheduler
    {
        private SynchronizationContext m_synchronizationContext;

        /// <summary>
        /// Constructs a SynchronizationContextTaskScheduler associated with <see cref="T:System.Threading.SynchronizationContext.Current"/> 
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">This constructor expects <see cref="T:System.Threading.SynchronizationContext.Current"/> to be set.</exception>
        internal SynchronizationContextTaskScheduler()
        {
            SynchronizationContext synContext = SynchronizationContext.Current;

            // make sure we have a synccontext to work with
            if (synContext == null)
            {
                throw new InvalidOperationException(Strings.TaskScheduler_FromCurrentSynchronizationContext_NoCurrent);
            }

            m_synchronizationContext = synContext;

        }

        /// <summary>
        /// Implemetation of <see cref="T:System.Threading.Tasks.TaskScheduler.QueueTask"/> for this scheduler class.
        /// 
        /// Simply posts the tasks to be executed on the associated <see cref="T:System.Threading.SynchronizationContext"/>.
        /// </summary>
        /// <param name="task"></param>
        // [SecurityCritical]
        protected internal override void QueueTask(Task task)
        {
            m_synchronizationContext.Post(s_postCallback, (object)task);
        }

        /// <summary>
        /// Implementation of <see cref="T:System.Threading.Tasks.TaskScheduler.TryExecuteTaskInline"/>  for this scheduler class.
        /// 
        /// The task will be executed inline only if the call happens within 
        /// the associated <see cref="T:System.Threading.SynchronizationContext"/>.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="taskWasPreviouslyQueued"></param>
        // [SecurityCritical]
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (SynchronizationContext.Current == m_synchronizationContext)
            {
                return TryExecuteTask(task);
            }
            else
                return false;
        }

        // not implemented
        // [SecurityCritical]
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return null;
        }

        /// <summary>
        /// Implementes the <see cref="T:System.Threading.Tasks.TaskScheduler.MaximumConcurrencyLevel"/> property for
        /// this scheduler class.
        /// 
        /// By default it returns 1, because a <see cref="T:System.Threading.SynchronizationContext"/> based
        /// scheduler only supports execution on a single thread.
        /// </summary>
        public override Int32 MaximumConcurrencyLevel
        {
            get
            {
                return 1;
            }
        }

        // preallocated SendOrPostCallback delegate
        private static SendOrPostCallback s_postCallback = new SendOrPostCallback(PostCallback);

        // this is where the actual task invocation occures
        private static void PostCallback(object obj)
        {
            Task task = (Task) obj;

            // calling ExecuteEntry with double execute check enabled because a user implemented SynchronizationContext could be buggy
            task.ExecuteEntry(true);
        }
    }

    /// <summary>
    /// Provides data for the event that is raised when a faulted <see cref="System.Threading.Tasks.Task"/>'s
    /// exception goes unobserved.
    /// </summary>
    /// <remarks>
    /// The Exception property is used to examine the exception without marking it
    /// as observed, whereas the <see cref="SetObserved"/> method is used to mark the exception
    /// as observed.  Marking the exception as observed prevents it from triggering exception escalation policy
    /// which, by default, terminates the process.
    /// </remarks>
    public class UnobservedTaskExceptionEventArgs : EventArgs
    {
        private AggregateException m_exception;
        internal bool m_observed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnobservedTaskExceptionEventArgs"/> class
        /// with the unobserved exception.
        /// </summary>
        /// <param name="exception">The Exception that has gone unobserved.</param>
        public UnobservedTaskExceptionEventArgs(AggregateException exception) { m_exception = exception; }

        /// <summary>
        /// Marks the <see cref="Exception"/> as "observed," thus preventing it
        /// from triggering exception escalation policy which, by default, terminates the process.
        /// </summary>
        public void SetObserved() { m_observed = true; }

        /// <summary>
        /// Gets whether this exception has been marked as "observed."
        /// </summary>
        public bool Observed { get { return m_observed; } }
        
        /// <summary>
        /// The Exception that went unobserved.
        /// </summary>
        public AggregateException Exception { get { return m_exception; } }
    }
}
