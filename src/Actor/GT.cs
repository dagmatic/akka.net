using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Dagmatic.Akka.Actor
{
    /// <summary>
    /// Fluent interface to <see cref="GT{TData}.GoalMachine.IGoalContext"/>
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class GT<TData> : MachineHost
    {
        protected GoalMachine Machine { get; set; }

        public event EventHandler<object> FailureReported;

        public override void OnFailure(object failureReason)
        {
            FailureReported?.Invoke(this, failureReason);
        }

        protected override void OnReceive(object message)
        {
            Machine.ProcessMessage(message);
        }

        /// <summary>
        /// Replace root (created by <see cref="Spawn"/>) with result of <see cref="factory"/>().
        /// Internally, creates a goal to execute <see cref="GoalMachine.IGoalContext.ReplaceRoot"/>
        /// </summary>
        /// <param name="factory"></param>
        /// <returns></returns>
        protected GoalMachine.IGoal Become(Func<GoalMachine.IGoal> factory)
            => new GoalMachine.FromActionFactoryG(() => ctx => ctx.ReplaceRoot(factory()));

        /// <summary>
        /// This goal becomes root and possible target for <see cref="Become"/>
        /// </summary>
        /// <param name="goal"></param>
        /// <returns></returns>
        protected GoalMachine.IGoal Spawn(GoalMachine.IGoal goal)
            => new GoalMachine.FromFuncG(ctx =>
            {
                ctx.IsRoot = true;
                ctx.ReplaceSelf(goal);
                return GoalStatus.Pending;
            });

        /// <summary>
        /// Runs action and completes with Success or with Failure if an exception is thrown.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected GoalMachine.ActionG Execute(Action<GoalMachine.IGoalContext> action)
            => new GoalMachine.ActionG(action);

        /// <summary>
        /// Dynamic creation of goals.
        /// </summary>
        /// <param name="factory"></param>
        /// <returns></returns>
        protected GoalMachine.IGoal Factory(Func<GoalMachine.IGoalContext, GoalMachine.IGoal> factory)
            => FromAction(ctx => ctx.ReplaceSelf(factory(ctx)));

        /// <summary>
        /// Ad-hoc implementation of <see cref="GT{TData}.GoalMachine.IGoal"/>
        /// </summary>
        /// <param name="action"></param>
        /// <param name="failOnException"></param>
        /// <returns></returns>
        protected GoalMachine.FromActionFactoryG FromAction(Action<GoalMachine.IGoalContext> action, bool failOnException = true)
            => new GoalMachine.FromActionFactoryG(() => action, failOnException);

        /// <summary>
        /// Ad-hoc implementation of <see cref="GT{TData}.GoalMachine.IGoal"/>
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        protected GoalMachine.FromFuncG FromFunction(Func<GoalMachine.IGoalContext, GoalStatus> func)
            => new GoalMachine.FromFuncG(func);

        /// <summary>
        /// Waits for @cond goal to complete, then waits for @then goal if Success, or for @else goal if Failure
        /// </summary>
        /// <param name="cond">Goal that decides whether @then or @else are selected</param>
        /// <param name="then">Reports Success if null</param>
        /// <param name="else">Reports Failure if null</param>
        /// <returns></returns>
        protected GoalMachine.IfG If(GoalMachine.IGoal cond, GoalMachine.IGoal @then = null, GoalMachine.IGoal @else = null)
            => new GoalMachine.IfG(cond, @then, @else);

        /// <summary>
        /// Instant selection of @then or @else
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="then"></param>
        /// <param name="else"></param>
        /// <returns></returns>
        protected GoalMachine.IfG If(Func<GoalMachine.IGoalContext, bool> pred, GoalMachine.IGoal @then = null, GoalMachine.IGoal @else = null)
            => new GoalMachine.IfG(Condition(pred), @then, @else);

        /// <summary>
        /// Instant decision between Success or Failure
        /// </summary>
        /// <param name="pred"></param>
        /// <returns></returns>
        protected GoalMachine.ConditionG Condition(Func<GoalMachine.IGoalContext, bool> pred)
            => new GoalMachine.ConditionG(pred);

        /// <summary>
        /// Waits for @pred to become true. Then if @goal is not null, runs it, otherwise report Success.
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="goal"></param>
        /// <returns></returns>
        protected GoalMachine.WhenG When(Func<GoalMachine.IGoalContext, bool> pred, GoalMachine.IGoal goal = null)
            => new GoalMachine.WhenG(pred, goal);

        /// <summary>
        /// Waits for @pred goal to complete with Success. If it reports Failure, reinitializes it and waits for next message. Then runs @goal if not null. Otherwise report Success.
        /// </summary>
        /// <param name="pred">Goal to complete with Success before proceeding.</param>
        /// <param name="goal">Child goal</param>
        /// <returns></returns>
        protected GoalMachine.WhenG When(GoalMachine.IGoal pred, GoalMachine.IGoal goal = null)
            => new GoalMachine.WhenG(pred, goal);

        /// <summary>
        /// Both goals must succeed to report Success, otherwise Report failure. Runs @goal1 first. If successful, runs @goal2.
        /// </summary>
        /// <param name="goal1"></param>
        /// <param name="goal2"></param>
        /// <returns></returns>
        protected GoalMachine.AndG And(GoalMachine.IGoal goal1, GoalMachine.IGoal goal2)
            => new GoalMachine.AndG(goal1, goal2);

        /// <summary>
        /// Runs @children in sequence, using @pred to report status.
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="children"></param>
        /// <returns></returns>
        protected GoalMachine.SequenceG Sequence(Func<IEnumerable<GoalStatus>, GoalStatus> pred, params GoalMachine.IGoal[] children)
            => new GoalMachine.SequenceG(pred, children);

        /// <summary>
        /// Runs @children in parallel, meaning, runs all children in supplied order with each message. The processing order is guaranteed. Uses @pred to report status.
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="children"></param>
        /// <returns></returns>
        protected GoalMachine.ParallelG Parallel(Func<IEnumerable<GoalStatus>, GoalStatus> pred, params GoalMachine.IGoal[] children)
            => new GoalMachine.ParallelG(pred, children);

        /// <summary>
        /// Runs children in sequence, but stops as soon as one reports Success.
        /// </summary>
        /// <param name="children"></param>
        /// <returns></returns>
        protected GoalMachine.SequenceG AnySucceed(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AnySucceed, children);

        /// <summary>
        /// Runs all children in sequence, then sees if any succeeded.
        /// </summary>
        /// <param name="children"></param>
        /// <returns></returns>
        protected GoalMachine.SequenceG AnySucceedThru(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AnySucceedThru, children);

        /// <summary>
        /// Runs children in sequence, but stops as soon as one reports Failure.
        /// </summary>
        /// <param name="children"></param>
        /// <returns></returns>
        protected GoalMachine.SequenceG AllSucceed(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AllSucceed, children);

        /// <summary>
        /// Runs all children in sequence, then sees if all succeeded.
        /// </summary>
        /// <param name="children"></param>
        /// <returns></returns>
        protected GoalMachine.SequenceG AllSucceedThru(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AllSucceedThru, children);

        /// <summary>
        /// Runs all children in sequence. Then reports Success.
        /// </summary>
        /// <param name="children"></param>
        /// <returns></returns>
        protected GoalMachine.SequenceG AllComplete(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AllComplete, children);

        /// <summary>
        /// Executes @action repeatedly with each message
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected GoalMachine.WhileActionG Loop(Action<GoalMachine.IGoalContext> action)
            => While(_ => true, action);

        /// <summary>
        /// Executes @body goal repeatedly until in reports Failure
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        protected GoalMachine.UntilG Loop(GoalMachine.IGoal body)
            => Until(When(_ => false), body);

        /// <summary>
        /// Execute @body goal repeatedly
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        protected GoalMachine.UntilG Forever(GoalMachine.IGoal body)
            => Until(When(_ => false), body, false);

        /// <summary>
        /// Execute @body goal repeatedly until @pred is false or @body completes with Failure
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        protected GoalMachine.UntilG While(Func<GoalMachine.IGoalContext, bool> pred, GoalMachine.IGoal body)
            => Until(When(ctx => !pred(ctx)), body);

        /// <summary>
        /// Execute @action repeatedly with each message, ignoring exceptions
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected GoalMachine.WhileActionG Forever(Action<GoalMachine.IGoalContext> action)
            => While(_ => true, action, false);

        /// <summary>
        /// Execute @action until @pred is false or @failOnException is true and @action throws exception.
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="action"></param>
        /// <param name="failOnException"></param>
        /// <returns></returns>
        protected GoalMachine.WhileActionG While(Func<GoalMachine.IGoalContext, bool> pred, Action<GoalMachine.IGoalContext> action, bool failOnException = true)
            => new GoalMachine.WhileActionG(pred, action);

        /// <summary>
        /// Execute @body goal until @pred is true or failOnBodyFailure is true and @body completes with Failure.
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="body"></param>
        /// <param name="failOnBodyFailure"></param>
        /// <returns></returns>
        protected GoalMachine.UntilG Until(GoalMachine.IGoal pred, GoalMachine.IGoal body, bool failOnBodyFailure = true)
            => new GoalMachine.UntilG(pred, body, failOnBodyFailure);

        /// <summary>
        /// Waits for @child to complete and then inverts result from Success to Failure and vice versa.
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        protected GoalMachine.NotG Not(GoalMachine.IGoal child)
            => new GoalMachine.NotG(child);

        /// <summary>
        /// Waits for @child to complete and then reports Success regardless of @child status.
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        protected GoalMachine.IGoal Pass(GoalMachine.IGoal child)
            => Then(child, Ok());

        /// <summary>
        /// Waits for @child to complete and then reports Failure regardless of @child status.
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        protected GoalMachine.FailG Fail(GoalMachine.IGoal child = null)
            => new GoalMachine.FailG(child);

        /// <summary>
        /// Waits for @child to complete and then executes @after.
        /// </summary>
        /// <param name="child"></param>
        /// <param name="after"></param>
        /// <returns></returns>
        protected GoalMachine.IGoal After(GoalMachine.IGoal child, GoalMachine.IGoal after)
            => Then(child, after);

        /// <summary>
        /// Makes sure to execute @goal not now, but on any subsequent message and context that satisfies @pred.
        /// </summary>
        /// <param name="goal">Runs after @pred is successful. Reports Success if null.</param>
        /// <param name="pred"></param>
        /// <returns></returns>
        protected GoalMachine.NextMessageG NextMessage(GoalMachine.IGoal goal = null, Func<GoalMachine.IGoalContext, bool> pred = null)
            => new GoalMachine.NextMessageG(goal, pred);

        /// <summary>
        /// Makes sure to execute @goal not now, but on any subsequent message that satisfies @pred.
        /// </summary>
        /// <param name="pred"></param>
        /// <param name="goal">Runs after @pred is successful. Reports Success if null.</param>
        /// <returns></returns>
        protected GoalMachine.NextMessageG NextMessage(Func<object, bool> pred, GoalMachine.IGoal goal = null)
            => NextMessage(goal, ctx => pred(ctx.CurrentMessage));

        /// <summary>
        /// Makes sure to execute @goal not now, but on any subsequent message that is if type {T}
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="goal">Reports Success if null</param>
        /// <returns></returns>
        protected GoalMachine.NextMessageG NextMessage<T>(GoalMachine.IGoal goal = null)
            => NextMessage(goal, ctx => ctx.CurrentMessage is T);

        /// <summary>
        /// Makes sure to execute @goal not now, but on any subsequent message that is if type {T} and satisfies @pred
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pred">Test against current message</param>
        /// <param name="goal">Runs after @pred is successful. Reports Success if null.</param>
        /// <returns></returns>
        protected GoalMachine.NextMessageG NextMessage<T>(Func<T, bool> pred, GoalMachine.IGoal goal = null)
            => NextMessage(goal, ctx => ctx.CurrentMessage is T && pred((T)ctx.CurrentMessage));

        /// <summary>
        /// Ensure that @pred is only checked once per message. When @pred is true, execute @goal.
        /// </summary>
        /// <param name="goal">Runs after @pred is successful. Reports Success if null.</param>
        /// <param name="pred">Test against context</param>
        /// <returns></returns>
        protected GoalMachine.OnMessageG OnMessage(GoalMachine.IGoal goal = null, Func<GoalMachine.IGoalContext, bool> pred = null)
            => new GoalMachine.OnMessageG(goal, pred);

        /// <summary>
        /// Ensure that @pred is only checked once per message. When @pred is true, execute @goal.
        /// </summary>
        /// <param name="pred">Test against current message</param>
        /// <param name="goal">Runs after @pred is successful. Reports Success if null.</param>
        /// <returns></returns>
        protected GoalMachine.OnMessageG OnMessage(Func<object, bool> pred, GoalMachine.IGoal goal = null)
            => OnMessage(goal, ctx => pred(ctx.CurrentMessage));

        /// <summary>
        /// Waits for message of type {T}. Then runs @goal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="goal">Runs after message of type {T} arrives. Reports Success if null.</param>
        /// <returns></returns>
        protected GoalMachine.OnMessageG OnMessage<T>(GoalMachine.IGoal goal = null)
            => OnMessage(goal, ctx => ctx.CurrentMessage is T);

        /// <summary>
        /// Ensure that @pred is only checked once per message of type {T}. When @pred is true, execute @goal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pred">Test against message of type {T}</param>
        /// <param name="goal">Runs after @pred is successful. Reports Success if null</param>
        /// <returns></returns>
        protected GoalMachine.OnMessageG OnMessage<T>(Func<T, bool> pred, GoalMachine.IGoal goal = null)
            => OnMessage(goal, ctx => ctx.CurrentMessage is T && pred((T)ctx.CurrentMessage));

        /// <summary>
        /// Run @child. Then run @then.
        /// </summary>
        /// <param name="child"></param>
        /// <param name="then"></param>
        /// <returns></returns>
        protected GoalMachine.ThenG Then(GoalMachine.IGoal child, GoalMachine.IGoal then)
            => new GoalMachine.ThenG(child, then);

        /// <summary>
        /// Run @child. If it does not complete within @delay, run @onTimeout.
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="child"></param>
        /// <param name="onTimeout">Reports Failure if null.</param>
        /// <returns></returns>
        protected GoalMachine.TimeoutG Timeout(TimeSpan delay, GoalMachine.IGoal child, GoalMachine.IGoal onTimeout = null)
            => new GoalMachine.TimeoutG(_ => delay, child, onTimeout ?? Fail());

        /// <summary>
        /// Dynamic delay selection. Run @child. If it does not complete within @delay, run @onTimeout.
        /// </summary>
        /// <param name="delay">This is run once to determine desired delay.</param>
        /// <param name="child"></param>
        /// <param name="onTimeout">Reports Failure if null.</param>
        /// <returns></returns>
        protected GoalMachine.TimeoutG Timeout(Func<GoalMachine.IGoalContext, TimeSpan> delay, GoalMachine.IGoal child, GoalMachine.IGoal onTimeout = null)
            => new GoalMachine.TimeoutG(delay, child, onTimeout ?? Fail());

        /// <summary>
        /// A goal that never completes.
        /// </summary>
        /// <returns></returns>
        protected GoalMachine.NeverG Never()
            => new GoalMachine.NeverG();

        /// <summary>
        /// Wait @delay, then execute @after.
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="after">Reports Success if null.</param>
        /// <returns></returns>
        protected GoalMachine.IGoal Delay(TimeSpan delay, GoalMachine.IGoal after = null)
            => Delay(_ => delay, after ?? Ok());

        /// <summary>
        /// Wait @delay(ctx), then execute @after.
        /// </summary>
        /// <param name="delay">This is run once to determine desired delay.</param>
        /// <param name="after"></param>
        /// <returns></returns>
        protected GoalMachine.IGoal Delay(Func<GoalMachine.IGoalContext, TimeSpan> delay, GoalMachine.IGoal after = null)
            => new GoalMachine.TimeoutG(delay, Never(), after ?? Ok());

        /// <summary>
        /// Instant Success.
        /// </summary>
        /// <returns></returns>
        protected GoalMachine.OkG Ok()
            => new GoalMachine.OkG();

        /// <summary>
        /// Must run this first. Submit all your goals in @goal. Initialize your user data with @data.
        /// </summary>
        /// <param name="goal"></param>
        /// <param name="data"></param>
        protected void StartWith(GoalMachine.IGoal goal, TData data)
        {
            Machine = new GoalMachine(this, data);
            Machine.Run(goal);
        }

        /// <summary>
        /// Is responsible for running <see cref="IGoalContext"/>s in response to messages.
        /// </summary>
        public class GoalMachine
        {
            private List<GoalContext> _contexts = new List<GoalContext>();

            public GoalMachine(IMachineHost host, TData data)
            {
                Host = host;
                Data = data;
            }

            public IMachineHost Host { get; }
            public object CurrentMessage { get; private set; }
            public ulong MessageCount { get; private set; }
            public TData Data { get; }

            /// <summary>
            /// Increments <see cref="MessageCount"/>, sets <see cref="CurrentMessage"/> and runs <see cref="IGoalContext"/>s
            /// </summary>
            /// <param name="message"></param>
            public void ProcessMessage(object message)
            {
                MessageCount++;
                CurrentMessage = message;

                foreach (var context in _contexts)
                {
                    GoalContext.Run(context);
                }
            }

            /// <summary>
            /// Wraps <see cref="goal"/> in <see cref="IGoalContext"/> and runs it.
            /// </summary>
            /// <param name="goal"></param>
            public void Run(IGoal goal)
            {
                var context = new GoalContext(this, goal);
                _contexts.Add(context);
                GoalContext.Run(context);
            }

            public event EventHandler<IGoal> Failed;

            /// <summary>
            /// <see cref="IGoal"/> use this to express their behavior. Goal environment is reactive, in response to messages. 
            /// Use properties and methods of this object to express goal behavior and to interact with outside world.
            /// </summary>
            public interface IGoalContext
            {
                /// <summary>
                /// <see cref="GoalMachine"/> runs through goal contexts every time it receive a message. This is the message.
                /// <see cref="GoalMachine"/> also runs first time when it's created. Then this message is null.
                /// </summary>
                object CurrentMessage { get; }
                /// <summary>
                /// Use this to schedule messages or to report failure.
                /// </summary>
                IMachineHost Host { get; }
                /// <summary>
                /// Use this to implement message sequencing logic.
                /// </summary>
                ulong MessageCount { get; }
                /// <summary>
                /// User defined data. Usually contains business logic.
                /// </summary>
                TData Data { get; }
                /// <summary>
                /// The view of goal children, indexed by position.
                /// </summary>
                IReadOnlyDictionary<int, IGoal> Children { get; }
                /// <summary>
                /// The view of goal child stats, indexed by position.
                /// </summary>
                IReadOnlyDictionary<int, GoalStatus> ChildStats { get; }
                /// <summary>
                /// Set this to report Success or Failure. Setting any other properties has no effect if this is set to Success and Failure.
                /// </summary>
                GoalStatus Status { get; set; }
                /// <summary>
                /// Set this to explain failure. If an exception occurs during <see cref="IGoal.Update"/>, it will be set here.
                /// </summary>
                object FailureReason { get; set; }
                /// <summary>
                /// Interacts with <see cref="ReplaceRoot"/>
                /// </summary>
                bool IsRoot { get; set; }
                /// <summary>
                /// Replace goal's children.
                /// </summary>
                /// <param name="children"></param>
                void SetChildren(params IGoal[] children);
                /// <summary>
                /// Check self and parents until <see cref="IsRoot"/> is true or the context has no parent. Then set this goal and discard all other children.
                /// </summary>
                /// <param name="goal"></param>
                void ReplaceRoot(IGoal goal);
                /// <summary>
                /// Replace current goal with this and discard all children.
                /// </summary>
                /// <param name="goal"></param>
                void ReplaceSelf(IGoal goal);
                /// <summary>
                /// Add a child with <see cref="IsRoot"/> set to true.
                /// </summary>
                /// <param name="goal"></param>
                void Spawn(IGoal goal);
                /// <summary>
                /// A convenience method that ensures the scheduled message will only be delivered to the goal that requested it.
                /// </summary>
                /// <param name="message"></param>
                /// <param name="delay"></param>
                void ScheduleMessage(object message, TimeSpan delay);
            }

            public class GoalContext : IGoalContext
            {
                public class ScheduledMessage
                {
                    public object Message;
                    public Guid Id;
                }

                private class ReadonlyListDictionary<C, T> : IReadOnlyDictionary<int, T>
                {
                    private List<C> _container;
                    private Func<C, T> _selector;
                    public ReadonlyListDictionary(List<C> container, Func<C, T> selector)
                    {
                        _container = container;
                        _selector = selector;
                    }

                    public T this[int key] => _selector(_container[key]);

                    public int Count => _container.Count;

                    public IEnumerable<int> Keys => Enumerable.Range(0, _container.Count);

                    public IEnumerable<T> Values => _container.Select(_selector);

                    public bool ContainsKey(int key) => _container.Count > key && key >= 0;

                    public IEnumerator<KeyValuePair<int, T>> GetEnumerator() => _container.Select((v, i) => new KeyValuePair<int, T>(i, _selector(v))).GetEnumerator();

                    public bool TryGetValue(int key, out T value)
                    {
                        if (ContainsKey(key))
                        {
                            value = this[key];
                            return true;
                        }

                        value = default(T);
                        return false;
                    }

                    IEnumerator IEnumerable.GetEnumerator()
                    {
                        return GetEnumerator();
                    }
                }

                private GoalMachine _machine;
                private ImmutableDictionary<Guid, IDisposable> _scheduled = ImmutableDictionary<Guid, IDisposable>.Empty;
                private Guid? _scheduledToRemove;
                private object _scheduledMessage;
                private ulong _scheduledCount = ulong.MinValue;

                public GoalContext(GoalMachine machine, IGoal goal, GoalContext parent = null)
                {
                    _machine = machine;
                    Goal = goal;
                    Goal.Initialize();
                    ChildContexts = new List<GoalContext>();
                    Children = new ReadonlyListDictionary<GoalContext, IGoal>(ChildContexts, c => c.Goal);
                    ChildStats = new ReadonlyListDictionary<GoalContext, GoalStatus>(ChildContexts, c => c.Status);
                    Data = machine.Data;
                    ParentContext = parent;
                }

                public object CurrentMessage
                {
                    get
                    {
                        if (_scheduledCount == _machine.MessageCount)
                            return _scheduledMessage;

                        return _machine.CurrentMessage;
                    }
                }

                public IMachineHost Host => _machine.Host;
                public ulong MessageCount => _machine.MessageCount;
                public GoalContext ParentContext { get; }
                public IGoal Goal { get; protected set; }
                public TData Data { get; }
                public GoalStatus Status { get; set; }
                public object FailureReason { get; set; }
                public IReadOnlyDictionary<int, IGoal> Children { get; }
                public IReadOnlyDictionary<int, GoalStatus> ChildStats { get; }
                public List<GoalContext> ChildContexts { get; protected set; }
                public bool IsRoot { get; set; }
                public IEnumerable<IGoal> ReplacedChildren { get; protected set; }
                public IGoal ReplacedRoot { get; protected set; }
                public IGoal ReplacedSelf { get; protected set; }
                public ImmutableDictionary<IGoal, GoalContext> ContextLookup { get; protected set; }
                public ImmutableList<IGoal> SpawnedGoals { get; set; } = ImmutableList<IGoal>.Empty;
                public ImmutableQueue<Tuple<object, TimeSpan>> ScheduledMessages { get; set; } = ImmutableQueue<Tuple<object, TimeSpan>>.Empty;

                public void SetChildren(params IGoal[] children)
                {
                    ReplacedChildren = children;
                }

                public void ReplaceRoot(IGoal goal)
                {
                    ReplacedRoot = goal;
                }

                public void ReplaceSelf(IGoal goal)
                {
                    ReplacedSelf = goal;
                }

                public void Spawn(IGoal goal)
                {
                    SpawnedGoals = SpawnedGoals.Add(goal);
                }

                public void ReplaceGoal(IGoal goal)
                {
                    Goal = goal;
                    Goal.Initialize();
                    ReplacedRoot = null;
                    ReplacedSelf = null;
                    ChildContexts.Clear();
                }

                public void ReplaceChildren()
                {
                    ChildContexts.Clear();
                    ChildContexts.AddRange(ReplacedChildren.Select(h => new GoalContext(_machine, h, this)));
                    ReplacedChildren = null;
                }

                public void SpawnGoals()
                {
                    ChildContexts.AddRange(SpawnedGoals.Select(h => new GoalContext(_machine, h, this) { IsRoot = true }));
                    SpawnedGoals = ImmutableList<IGoal>.Empty;
                }

                public void ScheduleMessage(object message, TimeSpan delay)
                {
                    ScheduledMessages = ScheduledMessages.Enqueue(Tuple.Create(message, delay));
                }

                private void ScheduleMessages()
                {
                    Tuple<object, TimeSpan> current;
                    while (!ScheduledMessages.IsEmpty)
                    {
                        ScheduledMessages = ScheduledMessages.Dequeue(out current);

                        var id = Guid.NewGuid();
                        var msg = new ScheduledMessage { Id = id, Message = current.Item1 };

                        _scheduled = _scheduled.Add(id, _machine.Host.ScheduleMessage(current.Item2, msg));
                    }
                }

                private void BeforeUpdate()
                {
                    ScheduledMessages = ImmutableQueue<Tuple<object, TimeSpan>>.Empty;

                    if (_scheduledCount < _machine.MessageCount && _scheduledToRemove.HasValue)
                    {
                        _scheduled = _scheduled.Remove(_scheduledToRemove.Value);
                        _scheduledToRemove = null;
                    }

                    var scheduled = _machine.CurrentMessage as ScheduledMessage;
                    if (scheduled == null)
                    {
                        _scheduledCount = _machine.MessageCount + 1;
                    }
                    else if (_scheduled.ContainsKey(scheduled.Id))
                    {
                        _scheduledToRemove = scheduled.Id;
                        _scheduledCount = _machine.MessageCount;
                        _scheduledMessage = scheduled.Message;
                    }
                }

                private void UpdateGoal()
                {
                    BeforeUpdate();

                    try
                    {
                        Goal.Update(this);
                    }
                    catch (Exception ex)
                    {
                        FailureReason = ex;
                        Status = GoalStatus.Failure;
                    }

                    if (FailureReason != null)
                    {
                        Host.OnFailure(FailureReason);

                        FailureReason = null;
                    }
                }

                public static void Run(GoalContext ctx)
                {
                    var stack = new Stack<ImmutableList<GoalContext>>();
                    stack.Push(ImmutableList<GoalContext>.Empty.Add(ctx));

                    while (stack.Count > 0)
                    {
                        var children = stack.Pop();

                        nextchildcheck:

                        while (children.Count > 0)
                        {
                            var child = children[0];

                            children = children.RemoveAt(0);

                            pendingcheck:

                            while (child.Status == GoalStatus.Pending)
                            {
                                var pendingChildren = ImmutableList<GoalContext>.Empty.AddRange(child.ChildContexts.Where(c => c.Status == GoalStatus.Pending));

                                if (pendingChildren.Count == 0)
                                {
                                    child.UpdateGoal();
                                }
                                else
                                {
                                    stack.Push(children);
                                    children = pendingChildren;

                                    goto nextchildcheck;
                                }

                                var childUpdated = false;

                                completecheck:

                                if (child.Status != GoalStatus.Pending)
                                {
                                    if (child.ParentContext != null)
                                    {
                                        children = stack.Pop();

                                        child = child.ParentContext;

                                        child.UpdateGoal();

                                        childUpdated = true;

                                        goto completecheck;
                                    }

                                    goto nextchildcheck;
                                }

                                child.ScheduleMessages();

                                if (child.ReplacedRoot != null)
                                {
                                    var replacedRoot = child.ReplacedRoot;
                                    child.ReplacedRoot = null;

                                    while (stack.Count > 0 && child.ParentContext != null && child.IsRoot == false)
                                    {
                                        child = child.ParentContext;
                                        children = stack.Pop();
                                    }

                                    child.ReplaceGoal(replacedRoot);
                                }
                                else if (child.ReplacedSelf != null)
                                {
                                    child.ReplaceGoal(child.ReplacedSelf);
                                }
                                else if (child.ReplacedChildren != null)
                                {
                                    child.ReplaceChildren();
                                }
                                else if (child.SpawnedGoals.IsEmpty == false)
                                {
                                    child.SpawnGoals();
                                }
                                else
                                {
                                    if (childUpdated)
                                        goto pendingcheck;

                                    break;
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// <see cref="IGoal"/> works with <see cref="IGoalContext"/> in its <see cref="Update"/> method to implement goal behavior. 
            /// Goals start as Pending, and will have their Update method invoked until they report Success or Failure.
            /// Goals are like <see cref="System.Threading.Tasks.Task"/>, but orders of magnitude more flexible.
            /// </summary>
            public interface IGoal
            {
                /// <summary>
                /// Goals are reusable. This method is called before each re-use. (usually in cycles).
                /// </summary>
                void Initialize();
                /// <summary>
                /// The point of interaction with goal context. Set properties and call methods on <see cref="IGoalContext"/> to express goal behavior. 
                /// <see cref="Update"/> is called when goal status is Pending and either
                /// 1) Goal has just been initialized
                /// 2) a new message comes
                /// 3) a child goal completes (with Success or Failure).
                /// </summary>
                /// <param name="ctx"></param>
                void Update(IGoalContext ctx);
            }

            public class GoalBase : IGoal
            {
                public virtual void Update(IGoalContext context) { }
                public virtual void Initialize() { }
            }

            public class FromActionFactoryG : GoalBase
            {
                private Func<Action<IGoalContext>> _action;
                private bool _failOnException;

                public FromActionFactoryG(Func<Action<IGoalContext>> action, bool failOnException = true)
                {
                    _action = action;
                    _failOnException = failOnException;
                }

                public override void Update(IGoalContext context)
                {
                    try
                    {
                        _action()(context);
                    }
                    catch (Exception ex)
                    {
                        if (_failOnException)
                        {
                            context.Status = GoalStatus.Failure;
                            context.FailureReason = ex;
                        }
                    }
                }
            }

            public class FromFuncG : GoalBase
            {
                private Func<IGoalContext, GoalStatus> _func;
                private bool _failOnException;

                public FromFuncG(Func<IGoalContext, GoalStatus> func, bool failOnException = true)
                {
                    _func = func;
                    _failOnException = failOnException;
                }

                public override void Update(IGoalContext context)
                {
                    try
                    {
                        context.Status = _func(context);
                    }
                    catch (Exception ex)
                    {
                        if (_failOnException)
                        {
                            context.Status = GoalStatus.Failure;
                            context.FailureReason = ex;
                        }
                    }
                }
            }

            public class ActionG : ActionGoalBase
            {
                public ActionG(Action<IGoalContext> action) : base(action) { }

                public override void Update(IGoalContext ctx)
                {
                    try
                    {
                        Action(ctx);

                        ctx.Status = GoalStatus.Success;
                    }
                    catch (Exception ex)
                    {
                        ctx.Status = GoalStatus.Failure;
                        ctx.FailureReason = ex;
                    }
                }
            }

            public class WhileActionG : ActionGoalBase
            {
                public WhileActionG(Func<IGoalContext, bool> pred, Action<IGoalContext> action, bool failOnException = true) : base(action)
                {
                    Pred = pred;
                    FailOnException = failOnException;
                }

                public Func<IGoalContext, bool> Pred { get; }

                public bool FailOnException { get; }

                public override void Update(IGoalContext ctx)
                {
                    try
                    {
                        if (Pred(ctx))
                        {
                            Action(ctx);
                        }
                        else
                        {
                            ctx.Status = GoalStatus.Success;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (FailOnException)
                        {
                            ctx.Status = GoalStatus.Failure;
                            ctx.FailureReason = ex;
                        }
                    }
                }
            }

            public class UntilG : GoalBase
            {
                private ulong _messageCount;

                public UntilG(IGoal pred, IGoal body, bool failOnBodyFailure = true)
                {
                    Pred = pred;
                    Body = body;
                    FailOnBodyFailure = failOnBodyFailure;
                }

                public IGoal Pred { get; protected set; }
                public IGoal Body { get; }
                public bool FailOnBodyFailure { get; }

                public override void Update(IGoalContext ctx)
                {
                    if (ctx.Children.Count == 0)
                    {
                        ctx.SetChildren(Pred, Body);
                    }
                    else
                    {
                        if (ctx.ChildStats[0] != GoalStatus.Pending)
                        {
                            ctx.Status = ctx.ChildStats[0];
                        }
                        else if (ctx.ChildStats[1] != GoalStatus.Pending)
                        {
                            if (ctx.ChildStats[1] == GoalStatus.Failure && FailOnBodyFailure)
                            {
                                ctx.Status = GoalStatus.Failure;
                            }
                            else
                            {
                                if (ctx.MessageCount > _messageCount)
                                {
                                    _messageCount = ctx.MessageCount;
                                    ctx.SetChildren(Pred, Body);
                                }
                                else
                                {
                                    ctx.ReplaceSelf(new NextMessageG(this));
                                }
                            }
                        }
                    }
                }
            }

            public class ActionGoalBase : GoalBase
            {
                public ActionGoalBase(Action<IGoalContext> action)
                {
                    Action = action;
                }

                public Action<IGoalContext> Action;
            }

            public class TimeoutG : GoalBase
            {
                public TimeoutG(Func<IGoalContext, TimeSpan> delay, IGoal worker, IGoal onTimeout)
                {
                    Delay = delay;
                    Worker = worker;
                    OnTimeout = onTimeout;
                }

                public Func<IGoalContext, TimeSpan> Delay { get; }
                public IGoal Worker { get; protected set; }
                public IGoal OnTimeout { get; }

                public override void Update(IGoalContext ctx)
                {
                    if (ctx.Children.Count == 0)
                    {
                        var timeoutId = Guid.NewGuid();
                        var timedout = false;
                        var success = false;
                        var token = ctx.Host.ScheduleMessage(Delay(ctx), new WFTimeout(timeoutId));

                        ctx.SetChildren(
                            new ThenG(
                                new ParallelG(GoalEx.AnyComplete,
                                    new ThenG(
                                        new IfG(Worker, new ActionG(_ => success = true), null),
                                        new ActionG(_ => token.Dispose())),
                                    new WhenG(x => (x.CurrentMessage as WFTimeout)?.Id == timeoutId, new ActionG(_ => timedout = true))),
                                new IfG(new ConditionG(_ => timedout), OnTimeout, new ConditionG(_ => success))));
                    }
                    else if (ctx.ChildStats[0] != GoalStatus.Pending)
                    {
                        ctx.Status = ctx.ChildStats[0];
                    }
                }
            }

            public class NeverG : GoalBase
            {
            }

            public class FailG : GoalBase
            {
                private IGoal _child;

                public FailG(IGoal child = null)
                {
                    _child = child;
                }

                public override void Update(IGoalContext context)
                {
                    if (_child == null)
                        context.Status = GoalStatus.Failure;

                    if (context.Children.Count == 0)
                    {
                        context.SetChildren(_child);
                    }
                    else
                    {
                        context.Status = GoalStatus.Failure;
                    }
                }
            }

            public class OkG : GoalBase
            {
                public override void Update(IGoalContext context)
                {
                    context.Status = GoalStatus.Success;
                }
            }

            public class IfG : GoalBase
            {
                public IfG(IGoal cond, IGoal @then, IGoal @else)
                {
                    Cond = cond;
                    Then = then;
                    Else = @else;
                }

                public IGoal Cond { get; protected set; }
                public IGoal Then { get; }
                public IGoal Else { get; }

                public override void Update(IGoalContext ctx)
                {
                    if (ctx.Children.Count == 0)
                    {
                        ctx.SetChildren(Cond);
                        return;
                    }

                    if (ctx.ChildStats[0] != GoalStatus.Pending)
                    {
                        if (ctx.ChildStats[0] == GoalStatus.Success)
                        {
                            if (Then != null)
                                ctx.ReplaceSelf(Then);
                            else
                                ctx.Status = GoalStatus.Success;
                        }
                        else
                        {
                            if (@Else != null)
                                ctx.ReplaceSelf(@Else);
                            else
                                ctx.Status = GoalStatus.Failure;
                        }
                    }
                }
            }

            public class ConditionG : GoalBase
            {
                private Func<IGoalContext, bool> _pred;
                private IGoal _goal;

                public ConditionG(Func<IGoalContext, bool> pred, IGoal goal = null)
                {
                    _pred = pred;
                    _goal = goal;
                }

                public override void Update(IGoalContext ctx)
                {
                    try
                    {
                        var status = _pred(ctx) ? GoalStatus.Success : GoalStatus.Failure;
                        if (status == GoalStatus.Success && _goal != null)
                        {
                            ctx.ReplaceSelf(_goal);
                        }
                        else
                        {
                            ctx.Status = status;
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.Status = GoalStatus.Failure;
                        ctx.FailureReason = ex;
                    }
                }
            }

            public class NextMessageG : GoalBase
            {
                private ulong? _prevMessageCount;

                public NextMessageG(IGoal goal, Func<IGoalContext, bool> pred = null)
                {
                    Goal = goal;
                    Pred = pred ?? (_ => true);
                }

                public IGoal Goal { get; }
                public Func<IGoalContext, bool> Pred { get; }

                public override void Initialize()
                {
                    _prevMessageCount = null;
                }

                public override void Update(IGoalContext ctx)
                {
                    _prevMessageCount = _prevMessageCount ?? ctx.MessageCount;

                    if (ctx.MessageCount > _prevMessageCount)
                    {
                        if (Pred(ctx))
                        {
                            if (Goal != null)
                                ctx.ReplaceSelf(Goal);
                            else
                                ctx.Status = GoalStatus.Success;
                        }
                        else
                        {
                            _prevMessageCount++;
                        }
                    }
                }
            }

            public class OnMessageG : GoalBase
            {
                private ulong _prevMessageCount;

                public OnMessageG(IGoal goal, Func<IGoalContext, bool> pred = null)
                {
                    Goal = goal;
                    Pred = pred ?? (_ => true);
                }

                public IGoal Goal { get; }
                public Func<IGoalContext, bool> Pred { get; }

                public override void Initialize()
                {
                    _prevMessageCount = ulong.MinValue;
                }

                public override void Update(IGoalContext ctx)
                {
                    if (ctx.MessageCount > _prevMessageCount)
                    {
                        if (Pred(ctx))
                        {
                            if (Goal != null)
                                ctx.ReplaceSelf(Goal);
                            else
                                ctx.Status = GoalStatus.Success;
                        }
                        else
                        {
                            _prevMessageCount = ctx.MessageCount;
                        }
                    }
                }
            }

            public class WhenG : GoalBase
            {
                private IGoal _pred;
                private IGoal _goal;

                public WhenG(Func<IGoalContext, bool> pred, IGoal goal = null)
                    : this(new ConditionG(pred), goal) { }

                public WhenG(IGoal pred, IGoal goal = null)
                {
                    _pred = pred;
                    _goal = goal;
                }

                public override void Update(IGoalContext ctx)
                {
                    if (ctx.Children.Count == 0)
                    {
                        ctx.SetChildren(_pred);
                        return;
                    }

                    if (ctx.ChildStats[0] == GoalStatus.Success)
                    {
                        if (_goal != null)
                        {
                            ctx.ReplaceSelf(_goal);
                        }
                        else
                        {
                            ctx.Status = GoalStatus.Success;
                        }
                    }
                    else
                    {
                        ctx.SetChildren(new NextMessageG(_pred));
                    }
                }
            }

            public class ThenG : BinaryG
            {
                public ThenG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override void OnGoal1Complete(GoalStatus goal1Status, IGoalContext ctx)
                {
                    ctx.ReplaceSelf(Goal2);
                }
            }

            public class AndG : BinaryG
            {
                public AndG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override void OnGoal1Complete(GoalStatus goal1Status, IGoalContext ctx)
                {
                    if (goal1Status == GoalStatus.Success)
                    {
                        ctx.ReplaceSelf(Goal2);
                    }
                    else
                    {
                        ctx.Status = GoalStatus.Failure;
                    }
                }
            }

            public class OrG : BinaryG
            {
                public OrG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override void OnGoal1Complete(GoalStatus goal1Status, IGoalContext ctx)
                {
                    if (goal1Status == GoalStatus.Failure)
                    {
                        ctx.ReplaceSelf(Goal2);
                    }
                    else
                    {
                        ctx.Status = GoalStatus.Success;
                    }
                }
            }

            public class XorG : BinaryG
            {
                public XorG(IGoal g1, IGoal g2) : base(g1, g2) { }

                protected override void OnGoal1Complete(GoalStatus goal1Status, IGoalContext ctx)
                {
                    if (goal1Status == GoalStatus.Failure)
                    {
                        ctx.ReplaceSelf(Goal2);
                    }
                    else
                    {
                        ctx.ReplaceSelf(new NotG(Goal2));
                    }
                }
            }

            public class NotG : GoalBase
            {
                public NotG(IGoal goal)
                {
                    Goal = goal;
                }

                public IGoal Goal { get; }

                public override void Update(IGoalContext ctx)
                {
                    if (ctx.Children.Count == 0)
                    {
                        ctx.SetChildren(Goal);
                    }
                    else if (ctx.ChildStats[0] != GoalStatus.Pending)
                    {
                        ctx.Status = ctx.ChildStats[0] == GoalStatus.Success ? GoalStatus.Failure : GoalStatus.Success;
                    }
                }
            }

            public class ExceptionG : GoalBase
            {
                public ExceptionG(Exception ex, IGoal goal)
                {
                    Exception = ex;
                    ExceptionSource = goal;
                }

                public override void Update(IGoalContext context)
                {
                    context.Status = GoalStatus.Failure;
                    context.FailureReason = Exception;
                }

                public Exception Exception { get; }
                public IGoal ExceptionSource { get; }
            }

            public abstract class BinaryG : GoalBase
            {
                protected BinaryG(IGoal g1, IGoal g2)
                {
                    Goal1 = g1;
                    Goal2 = g2;
                }

                public IGoal Goal1 { get; protected set; }
                public IGoal Goal2 { get; }

                public override void Update(IGoalContext ctx)
                {

                    if (ctx.Children.Count == 0)
                    {
                        ctx.SetChildren(Goal1);
                    }
                    else
                    {
                        if (ctx.ChildStats[0] != GoalStatus.Pending)
                        {
                            OnGoal1Complete(ctx.ChildStats[0], ctx);
                        }
                    }
                }

                protected abstract void OnGoal1Complete(GoalStatus goal1Status, IGoalContext ctx);
            }

            public class ParallelG : GoalBase
            {
                private IGoal[] _goals;
                private Func<IEnumerable<GoalStatus>, GoalStatus> _statusFunc;

                public ParallelG(Func<IEnumerable<GoalStatus>, GoalStatus> statusFunc, params IGoal[] goals)
                {
                    _statusFunc = statusFunc;
                    _goals = goals;
                }

                public override void Update(IGoalContext ctx)
                {
                    if (ctx.Children.Count == 0 && _goals.Length > 0)
                    {
                        ctx.SetChildren(_goals);
                    }
                    else
                    {
                        ctx.Status = _statusFunc(ctx.ChildStats.Values);
                    }
                }
            }

            public class SequenceG : GoalBase
            {
                private IGoal[] _goals;
                private Func<IEnumerable<GoalStatus>, GoalStatus> _statusFunc;
                private List<GoalStatus> _stats = new List<GoalStatus>();
                private int _index = -1;

                public SequenceG(Func<IEnumerable<GoalStatus>, GoalStatus> statusFunc, params IGoal[] goals)
                {
                    _statusFunc = statusFunc;
                    _goals = goals;
                }

                public override void Initialize()
                {
                    _index = -1;
                    _stats.Clear();
                }

                public override void Update(IGoalContext ctx)
                {
                    if (_index < 0)
                    {
                        if (_goals.Length > 0)
                        {
                            ctx.SetChildren(_goals[0]);
                            _index = 0;
                            return;
                        }
                    }

                    if (ctx.Children.Count > 0)
                    {
                        if (ctx.ChildStats[0] == GoalStatus.Pending)
                            return;

                        _stats.Add(ctx.ChildStats[0]);
                    }

                    ctx.Status = _statusFunc(_stats.Concat(Enumerable.Repeat(GoalStatus.Pending, _goals.Length - _index - 1)));

                    if (ctx.Status == GoalStatus.Pending)
                    {
                        if (_index < _goals.Length - 1)
                        {
                            _index++;

                            ctx.SetChildren(_goals[_index]);
                        }
                        else
                        {
                            ctx.SetChildren();
                        }
                    }
                }
            }
        }
    }

    public enum GoalStatus
    {
        Pending,
        Success,
        Failure
    }

    public static class GoalEx
    {
        /// <summary>
        /// Run goals until all succeed or any fail.
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public static GoalStatus AllSucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAllSucceed);
        }

        /// <summary>
        /// Run all goals, then see if they all succeeded.
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public static GoalStatus AllSucceedThru(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAllSucceedThru);
        }

        public static GoalStatus FirstSucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcFirstSucceed);
        }

        /// <summary>
        /// Run goals until one succeeds, or to the end.
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public static GoalStatus AnySucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAnySucceed);
        }

        /// <summary>
        /// Run goals to the end, then see if any succeeded.
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public static GoalStatus AnySucceedThru(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAnySucceedThru);
        }

        public static GoalStatus AllComplete(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAllComplete);
        }

        public static GoalStatus AnyComplete(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAnyComplete);
        }

        private static GoalStatus GetStatus(IEnumerable<GoalStatus> states, CalcStatus calc)
        {
            bool hasFailure = false;
            bool hasSuccess = false;
            bool hasPending = false;
            bool hasAny = false;

            foreach (var s in states)
            {
                hasFailure |= s == GoalStatus.Failure;
                hasSuccess |= s == GoalStatus.Success;
                hasPending |= s == GoalStatus.Pending;
                hasAny = true;
            }

            return calc(hasFailure, hasSuccess, hasPending, hasAny);
        }

        private static GoalStatus CalcAllSucceed(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny || hasFailure
                ? GoalStatus.Failure
                : hasPending
                    ? GoalStatus.Pending
                    : GoalStatus.Success;
        }

        private static GoalStatus CalcAllSucceedThru(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasPending
                    ? GoalStatus.Pending
                    : hasFailure
                        ? GoalStatus.Failure
                        : GoalStatus.Success;
        }

        private static GoalStatus CalcFirstSucceed(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny || hasFailure
                ? GoalStatus.Failure
                : hasSuccess
                    ? GoalStatus.Success
                    : GoalStatus.Pending;
        }

        private static GoalStatus CalcAllComplete(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasPending
                    ? GoalStatus.Pending
                    : GoalStatus.Success;
        }

        private static GoalStatus CalcAnyComplete(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasFailure || hasSuccess
                    ? GoalStatus.Success
                    : GoalStatus.Pending;
        }

        private static GoalStatus CalcAnySucceed(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasSuccess
                    ? GoalStatus.Success
                    : hasPending
                        ? GoalStatus.Pending
                        : GoalStatus.Failure;
        }

        private static GoalStatus CalcAnySucceedThru(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny)
        {
            return !hasAny
                ? GoalStatus.Failure
                : hasPending
                    ? GoalStatus.Pending
                    : hasSuccess
                        ? GoalStatus.Success
                        : GoalStatus.Failure;
        }

        private delegate GoalStatus CalcStatus(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny);

    }
}
