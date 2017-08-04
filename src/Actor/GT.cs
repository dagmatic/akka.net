using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Dagmatic.Akka.Actor
{
    public class GT<TData> : MachineHost
    {
        protected GoalMachine Machine { get; set; }

        protected override void OnReceive(object message)
        {
            Machine.ProcessMessage(message);
        }

        protected GoalMachine.IGoal Become(Func<GoalMachine.IGoal> factory)
            => new GoalMachine.FromActionFactoryG(() => ctx => ctx.ReplaceRoot(factory()));

        protected GoalMachine.IGoal Spawn(GoalMachine.IGoal goal)
            => new GoalMachine.FromFuncG(ctx =>
            {
                ctx.IsRoot = true;
                ctx.ReplaceSelf(goal);
                return GoalStatus.Pending;
            });

        protected GoalMachine.ActionG Execute(Action<GoalMachine.IGoalContext> action)
            => new GoalMachine.ActionG(action);

        protected GoalMachine.IGoal Factory(Func<GoalMachine.IGoalContext, GoalMachine.IGoal> factory)
            => FromAction(ctx => ctx.ReplaceSelf(factory(ctx)));

        protected GoalMachine.FromActionFactoryG FromAction(Action<GoalMachine.IGoalContext> action, bool failOnException = true)
            => new GoalMachine.FromActionFactoryG(() => action, failOnException);

        protected GoalMachine.FromFuncG FromFunction(Func<GoalMachine.IGoalContext, GoalStatus> func)
            => new GoalMachine.FromFuncG(func);

        protected GoalMachine.IfG If(GoalMachine.IGoal cond, GoalMachine.IGoal @then = null, GoalMachine.IGoal @else = null)
            => new GoalMachine.IfG(cond, @then, @else);

        protected GoalMachine.IfG If(Func<GoalMachine.IGoalContext, bool> pred, GoalMachine.IGoal @then = null, GoalMachine.IGoal @else = null)
            => new GoalMachine.IfG(Condition(pred), @then, @else);

        protected GoalMachine.ConditionG Condition(Func<GoalMachine.IGoalContext, bool> pred)
            => new GoalMachine.ConditionG(pred);

        protected GoalMachine.WhenG When(Func<GoalMachine.IGoalContext, bool> pred, GoalMachine.IGoal goal = null)
            => new GoalMachine.WhenG(pred, goal);

        protected GoalMachine.WhenG When(GoalMachine.IGoal pred, GoalMachine.IGoal goal = null)
            => new GoalMachine.WhenG(pred, goal);

        protected GoalMachine.AndG And(GoalMachine.IGoal goal1, GoalMachine.IGoal goal2)
            => new GoalMachine.AndG(goal1, goal2);

        protected GoalMachine.SequenceG Sequence(Func<IEnumerable<GoalStatus>, GoalStatus> pred, params GoalMachine.IGoal[] children)
            => new GoalMachine.SequenceG(pred, children);

        protected GoalMachine.ParallelG Parallel(Func<IEnumerable<GoalStatus>, GoalStatus> pred, params GoalMachine.IGoal[] children)
            => new GoalMachine.ParallelG(pred, children);

        protected GoalMachine.SequenceG AnySucceed(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AnySucceed, children);

        protected GoalMachine.SequenceG AllSucceed(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AllSucceed, children);

        protected GoalMachine.SequenceG AllComplete(params GoalMachine.IGoal[] children)
            => Sequence(GoalEx.AllComplete, children);

        protected GoalMachine.WhileActionG Loop(Action<GoalMachine.IGoalContext> action)
            => While(_ => true, action);

        protected GoalMachine.UntilG Loop(GoalMachine.IGoal body)
            => Until(When(_ => false), body);

        protected GoalMachine.UntilG Forever(GoalMachine.IGoal body)
            => Until(When(_ => false), body, false);

        protected GoalMachine.UntilG While(Func<GoalMachine.IGoalContext, bool> pred, GoalMachine.IGoal body)
            => Until(When(ctx => !pred(ctx)), body);

        protected GoalMachine.WhileActionG Forever(Action<GoalMachine.IGoalContext> action)
            => While(_ => true, action, false);

        protected GoalMachine.WhileActionG While(Func<GoalMachine.IGoalContext, bool> pred, Action<GoalMachine.IGoalContext> action, bool failOnException = true)
            => new GoalMachine.WhileActionG(pred, action);

        protected GoalMachine.UntilG Until(GoalMachine.IGoal pred, GoalMachine.IGoal body, bool failOnBodyFailure = true)
            => new GoalMachine.UntilG(pred, body, failOnBodyFailure);

        protected GoalMachine.NotG Not(GoalMachine.IGoal child)
            => new GoalMachine.NotG(child);

        protected GoalMachine.IGoal Pass(GoalMachine.IGoal child)
            => Then(child, Ok());

        protected GoalMachine.FailG Fail(GoalMachine.IGoal child = null)
            => new GoalMachine.FailG(child);

        protected GoalMachine.IGoal After(GoalMachine.IGoal child, GoalMachine.IGoal after)
            => Then(child, after);

        protected GoalMachine.NextMessageG NextMessage(GoalMachine.IGoal goal = null, Func<GoalMachine.IGoalContext, bool> pred = null)
            => new GoalMachine.NextMessageG(goal, pred);

        protected GoalMachine.NextMessageG NextMessage(Func<object, bool> pred, GoalMachine.IGoal goal = null)
            => NextMessage(goal, ctx => pred(ctx.CurrentMessage));

        protected GoalMachine.NextMessageG NextMessage<T>(GoalMachine.IGoal goal = null)
            => NextMessage(goal, ctx => ctx.CurrentMessage is T);

        protected GoalMachine.NextMessageG NextMessage<T>(Func<T, bool> pred, GoalMachine.IGoal goal = null)
            => NextMessage(goal, ctx => ctx.CurrentMessage is T && pred((T)ctx.CurrentMessage));

        protected GoalMachine.OnMessageG OnMessage(GoalMachine.IGoal goal = null, Func<GoalMachine.IGoalContext, bool> pred = null)
            => new GoalMachine.OnMessageG(goal, pred);

        protected GoalMachine.OnMessageG OnMessage(Func<object, bool> pred, GoalMachine.IGoal goal = null)
            => OnMessage(goal, ctx => pred(ctx.CurrentMessage));

        protected GoalMachine.OnMessageG OnMessage<T>(GoalMachine.IGoal goal = null)
            => OnMessage(goal, ctx => ctx.CurrentMessage is T);

        protected GoalMachine.OnMessageG OnMessage<T>(Func<T, bool> pred, GoalMachine.IGoal goal = null)
            => OnMessage(goal, ctx => ctx.CurrentMessage is T && pred((T)ctx.CurrentMessage));

        protected GoalMachine.ThenG Then(GoalMachine.IGoal child, GoalMachine.IGoal then)
            => new GoalMachine.ThenG(child, then);

        protected GoalMachine.TimeoutG Timeout(TimeSpan delay, GoalMachine.IGoal child, GoalMachine.IGoal onTimeout = null)
            => new GoalMachine.TimeoutG(_ => delay, child, onTimeout ?? Fail());

        protected GoalMachine.TimeoutG Timeout(Func<GoalMachine.IGoalContext, TimeSpan> delay, GoalMachine.IGoal child, GoalMachine.IGoal onTimeout = null)
            => new GoalMachine.TimeoutG(delay, child, onTimeout ?? Fail());

        protected GoalMachine.NeverG Never()
            => new GoalMachine.NeverG();

        protected GoalMachine.IGoal Delay(TimeSpan delay, GoalMachine.IGoal after = null)
            => Delay(_ => delay, after ?? Ok());

        protected GoalMachine.IGoal Delay(Func<GoalMachine.IGoalContext, TimeSpan> delay, GoalMachine.IGoal after = null)
            => new GoalMachine.TimeoutG(delay, Never(), after ?? Ok());

        protected GoalMachine.OkG Ok()
            => new GoalMachine.OkG();


        protected void StartWith(GoalMachine.IGoal goal, TData data)
        {
            Machine = new GoalMachine(this, data);
            Machine.Run(goal);
        }

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

            public void ProcessMessage(object message)
            {
                MessageCount++;
                CurrentMessage = message;

                foreach (var context in _contexts)
                {
                    GoalContext.Run(context);
                }
            }

            public void Run(IGoal goal)
            {
                var context = new GoalContext(this, goal);
                _contexts.Add(context);
                GoalContext.Run(context);
            }

            public event EventHandler<IGoal> Failed;

            public interface IGoalContext
            {
                object CurrentMessage { get; }
                IMachineHost Host { get; }
                ulong MessageCount { get; }
                IGoal Parent { get; }
                TData Data { get; }
                IReadOnlyDictionary<int, IGoal> Children { get; }
                IReadOnlyDictionary<int, GoalStatus> ChildStats { get; }
                GoalStatus Status { get; set; }
                object FailureReason { get; set; }
                bool IsRoot { get; set; }
                void SetChildren(params IGoal[] children);
                void ReplaceRoot(IGoal goal);
                void ReplaceSelf(IGoal goal);
                void Spawn(IGoal goal);
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
                public IGoal Parent { get; }
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

                    Goal.Update(this);
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

            public interface IGoal
            {
                void Initialize();
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
                                ctx.SetChildren(Pred, Body);
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
                        var token = ctx.Host.ScheduleMessage(Delay(ctx), new WFTimeout(timeoutId));

                        ctx.SetChildren(
                            new SequenceG(GoalEx.AllComplete,
                                new ParallelG(GoalEx.AnyComplete,
                                    new SequenceG(GoalEx.AllComplete,
                                        Worker,
                                        new ActionG(_ => token.Dispose())),
                                    new WhenG(x => (x.CurrentMessage as WFTimeout)?.Id == timeoutId, new ActionG(_ => timedout = true))),
                                new ConditionG(_ => timedout, OnTimeout)));
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
        public static GoalStatus AllSucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAllSucceed);
        }

        public static GoalStatus FirstSucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcFirstSucceed);
        }

        public static GoalStatus AnySucceed(this IEnumerable<GoalStatus> states)
        {
            return GetStatus(states, CalcAnySucceed);
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

        private delegate GoalStatus CalcStatus(bool hasFailure, bool hasSuccess, bool hasPending, bool hasAny);

    }
}
