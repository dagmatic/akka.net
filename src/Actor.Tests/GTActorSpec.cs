﻿using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using Akka.Util;
using Akka.Util.Internal;
using Dagmatic.Akka.Actor;
using FluentAssertions;
using Xunit;
using Random = System.Random;

namespace Dagmatic.Akka.Tests.Actor
{
    public class GTActorSpec : AkkaSpec
    {
        #region Actors

        public class ConstructorIncrement : GT<AtomicCounter>
        {
            public ConstructorIncrement(AtomicCounter counter, TestLatch latch)
            {
                counter.IncrementAndGet();
                latch.CountDown();
            }
        }

        public class ExecuteIncrement : GT<AtomicCounter>
        {
            public ExecuteIncrement(AtomicCounter counter, TestLatch latch)
            {
                StartWith(Execute(cxt =>
                {
                    cxt.Data.IncrementAndGet();
                }), counter);

                latch.CountDown();
            }
        }

        public class OneEcho : GT<object>
        {
            public OneEcho()
            {
                StartWith(NextMessage(
                    Execute(ctx =>
                    {
                        Sender.Tell(ctx.CurrentMessage);
                    })), null);
            }
        }

        public class NestedEcho : GT<object>
        {
            public NestedEcho()
            {
                StartWith(
                    NextMessage(
                        NextMessage(
                            Execute(ctx =>
                            {
                                Sender.Tell(ctx.CurrentMessage);
                            }))), null);
            }
        }

        public class SequenceOne : GT<List<string>>
        {
            public SequenceOne(List<string> pipe, TestLatch latch)
            {
                StartWith(AllSucceed(Execute(ctx => ctx.Data.Add("1"))), pipe);

                latch.CountDown();
            }
        }

        public class SequenceTwo : GT<List<string>>
        {
            public SequenceTwo(List<string> pipe, TestLatch latch)
            {
                StartWith(AllSucceed(
                    Execute(ctx => ctx.Data.Add("1")),
                    Execute(ctx => ctx.Data.Add("2"))), pipe);

                latch.CountDown();
            }
        }

        public class SequenceReceive : GT<List<string>>
        {
            public SequenceReceive(List<string> pipe, TestLatch latch)
            {
                StartWith(AllSucceed(
                    Execute(ctx => ctx.Data.Add("1")),
                    OnMessage(Execute(ctx => ctx.Data.Add(ctx.CurrentMessage as string))),
                    Execute(ctx => latch.CountDown())), pipe);
            }
        }

        public class SequenceReceiveSequenceReceive : GT<List<string>>
        {
            public SequenceReceiveSequenceReceive(List<string> pipe, TestLatch latch)
            {
                StartWith(AllSucceed(
                    NextMessage(Execute(ctx => ctx.Data.Add(ctx.CurrentMessage as string))),
                    Execute(ctx => ctx.Data.Add("2")),
                    AllSucceed(
                        NextMessage(AllSucceed(
                            Execute(ctx => ctx.Data.Add(ctx.CurrentMessage as string)),
                            NextMessage(Execute(ctx => ctx.Data.Add(ctx.CurrentMessage as string)))
                        ))
                    ),
                    Execute(ctx => latch.CountDown())), pipe);
            }
        }

        public class LoopEcho : GT<object>
        {
            public LoopEcho()
            {
                StartWith(Loop(ctx =>
                    {
                        if (ctx.CurrentMessage != null)
                            Sender.Tell(ctx.CurrentMessage);
                    }
                ), null);
            }
        }

        public class SequenceParallelExecute : GT<AtomicCounter>
        {
            public SequenceParallelExecute(AtomicCounter counter, TestLatch latch)
            {
                StartWith(AllSucceed(
                    Parallel(ss => ss.AllSucceed(),
                        Execute(ctx => ctx.Data.GetAndAdd(1)),
                        Execute(ctx => ctx.Data.GetAndAdd(4))),
                    Execute(ctx => latch.CountDown())), counter);
            }
        }

        public class SequenceParallelReceive : GT<AtomicCounter>
        {
            public SequenceParallelReceive(AtomicCounter counter, TestLatch latch)
            {
                StartWith(AllSucceed(
                    Parallel(ss => ss.AllSucceed(),
                        NextMessage(Execute(ctx => ctx.Data.GetAndAdd(1))),
                        NextMessage(Execute(ctx => ctx.Data.GetAndAdd(4)))),
                    Execute(ctx => latch.CountDown())), counter);
            }
        }

        public class ParallelLoopReceive : GT<TestLatch>
        {
            private static Random _rand = new Random((int)DateTime.Now.Ticks);

            private Queue<string> _msgs1 = new Queue<string>();
            private Queue<string> _msgs2 = new Queue<string>();

            public ParallelLoopReceive(TestLatch latch)
            {
                StartWith(
                    Parallel(ss => ss.AllSucceed(),
                        Loop(NextMessage(Execute(Process1))),
                        Loop(NextMessage(Execute(Process2)))), latch);
            }

            public void Process1(GoalMachine.IGoalContext ctx)
            {
                var str = ctx.CurrentMessage as string;
                if (str != null)
                {
                    _msgs1.Enqueue(str);
                    Self.Tell(new InternalMessage(), Sender);
                }
            }

            public void Process2(GoalMachine.IGoalContext ctx)
            {
                var str = ctx.CurrentMessage as string;
                if (str != null)
                {
                    _msgs2.Enqueue(str);
                }

                var intmsg = ctx.CurrentMessage as InternalMessage;
                if (intmsg != null)
                {
                    Sender.Tell($"1{_msgs1.Dequeue()}2{_msgs2.Dequeue()}");
                }
            }

            public class InternalMessage
            {
            }
        }

        public class SequenceReceiveReverseThree : GT<TestLatch>
        {
            private Stack<object> _messages = new Stack<object>();

            public SequenceReceiveReverseThree()
            {
                StartWith(
                    NextMessage(
                        AllSucceed(
                            Execute(ctx => _messages.Push(ctx.CurrentMessage)),
                            NextMessage(
                                AllSucceed(
                                    Execute(ctx => _messages.Push(ctx.CurrentMessage)),
                                    NextMessage(AllSucceed(
                                        Execute(ctx => _messages.Push(ctx.CurrentMessage)),
                                        Execute(ReplyEcho))),
                                    Execute(ReplyEcho))),
                            Execute(ReplyEcho))), null);
            }

            private void ReplyEcho(GoalMachine.IGoalContext ctx) => Sender.Tell(_messages.Pop());
        }

        public class BecomePingPong : GT<List<string>>
        {
            private TestLatch _latch;

            public BecomePingPong(List<string> data, TestLatch latch)
            {
                _latch = latch;

                StartWith(OnMessage<string>(o => o.Equals("RUN"), Become(Ping)), data);
            }

            private GoalMachine.IGoal Done() =>
                Condition(_ =>
                {
                    _latch.CountDown();
                    return _latch.IsOpen;
                });

            private GoalMachine.IGoal WithDoneAndDone(GoalMachine.IGoal wf) =>
                AllSucceed(
                    AnySucceed(
                        Done(),
                        wf),
                    Execute(_ => Sender.Tell("DONE")));

            private GoalMachine.IGoal Ping() =>
                WithDoneAndDone(
                    OnMessage<string>(s => s == "PING",
                        AllSucceed(
                            Execute(ctx =>
                            {
                                ctx.Data.Add("PING");
                                Self.Tell("PONG", Sender);
                            }),
                            Become(Pong))));

            private GoalMachine.IGoal Pong() =>
                WithDoneAndDone(
                    OnMessage<string>(s => s == "PONG",
                        AllSucceed(
                            Execute(ctx =>
                            {
                                ctx.Data.Add("PONG");
                                Self.Tell("PING", Sender);
                            }),
                            Become(Ping))));
        }

        public class ParallelBecomer : GT<List<string>>
        {
            public ParallelBecomer()
            {
                StartWith(
                    Parallel(ss => ss.AllSucceed(),
                        Loop(ctx =>
                        {
                            if (ctx.CurrentMessage == "THERE?") Sender.Tell("HERE!");
                        }),
                        Parallel(ss => ss.AllSucceed(),
                            OnMessage<string>(s => s == "KILL",
                                Become(() =>
                                    AllSucceed(
                                        Execute(_ => Sender.Tell("I TRIED")),
                                        NextMessage<string>(s => s == "THERE?", Execute(_ => Sender.Tell("I ATE HIM")))))))), null);
            }
        }

        public class SpawnConfirm : GT<object>
        {
            public SpawnConfirm()
            {
                StartWith(
                    Parallel(ss => ss.AllSucceed(),
                        Loop(ctx =>
                        {
                            if (ctx.CurrentMessage == "THERE?") Sender.Tell("HERE!");
                        }),
                        Spawn(
                            OnMessage<string>(s => s.Equals("KILL"),
                                Become(() =>
                                    AllSucceed(
                                        Execute(_ => Sender.Tell("I TRIED")),
                                        NextMessage<string>(s => s == "THERE?", Execute(_ => Sender.Tell("I ATE HIM")))))))), null);
            }
        }

        public class LoopSpawn : GT<object>
        {
            public LoopSpawn()
            {
                StartWith(
                    Karu(), null);
            }

            GoalMachine.IGoal Karu() =>
                NextMessage(
                    AllSucceed(
                        Execute(_ => Sender.Tell("KARU")),
                        Become(Sel)));

            GoalMachine.IGoal Sel() =>
                NextMessage(
                    AllSucceed(
                        Execute(_ => Sender.Tell("SEL")),
                        Become(Karu)));
        }

        public class AllConditions : GT<object>
        {
            public AllConditions()
            {
                StartWith(
                    NextMessage(
                        AnySucceed(
                            Condition(x => x.CurrentMessage.Equals("NOT GO")),
                            AllSucceed(
                                Condition(x => x.CurrentMessage.Equals("GO")),
                                Execute(_ => Sender.Tell("I GO")),
                                NextMessage(
                                    If(x => x.CurrentMessage.Equals("LEFT"),
                                        AllSucceed(
                                            Execute(_ => Sender.Tell("I LEFT")),
                                            NextMessage(
                                                If(x => x.CurrentMessage.Equals("LEFT"),
                                                    @else: AllSucceed(
                                                        Execute(_ => Sender.Tell("I RIGHT")),
                                                        Loop(x =>
                                                            {
                                                                if (x.CurrentMessage.Equals("YES"))
                                                                    Sender.Tell("I YES");
                                                                else
                                                                    Sender.Tell("I NO");
                                                            })))))))))), null);
            }
        }

        public class FailFails : GT<object>
        {
            public FailFails()
            {
                StartWith(
                    AnySucceed(
                        Fail(),
                        NextMessage(Execute(_ => Sender.Tell(_.CurrentMessage)))), null);
            }
        }

        public class NotFailSucceeds : GT<object>
        {
            public NotFailSucceeds()
            {
                StartWith(
                    AnySucceed(
                        Not(Fail()),
                        NextMessage(Execute(_ => Sender.Tell(_.CurrentMessage)))), null);
            }
        }

        public class FailAfterSuccess : GT<object>
        {
            public FailAfterSuccess(TestLatch latch, AtomicCounter counter)
            {
                StartWith(
                    AllSucceed(
                        AnySucceed(
                            After(
                                Execute(_ => counter.GetAndIncrement()),
                                Fail()),
                            Execute(_ => counter.GetAndIncrement())),
                        Execute(_ => latch.CountDown())), null);
            }
        }

        public class SuccessAfterFailure : GT<object>
        {
            public SuccessAfterFailure(TestLatch latch, AtomicCounter counter)
            {
                StartWith(
                    AllSucceed(
                        AllComplete(
                            After(
                                AllSucceed(
                                    Execute(_ => counter.GetAndIncrement()),
                                    Fail()),
                                Not(Fail())),
                            Execute(_ => counter.GetAndIncrement())),
                        Execute(_ => latch.CountDown())), null);
            }
        }

        public class LongTimeoutShortTask : GT<object>
        {
            public LongTimeoutShortTask(TestLatch latch, AtomicCounter counter)
            {
                StartWith(
                    Loop(
                        NextMessage(s => s.Equals("RUN"),
                            After(
                                Timeout(TimeSpan.FromMilliseconds(500),
                                    Delay(100.Milliseconds(), Execute(_ => counter.GetAndIncrement())),
                                    Execute(_ =>
                                    {
                                        counter.GetAndDecrement();
                                        Sender.Tell("TIMEOUT");
                                    })),
                                Execute(_ => latch.CountDown())))), null);
            }
        }

        public class ShortTimeoutLongTask : GT<object>
        {
            public ShortTimeoutLongTask(TestLatch latch, AtomicCounter counter)
            {
                StartWith(
                    Loop(
                        NextMessage(s => s.Equals("RUN"),
                            After(
                                Timeout(TimeSpan.FromMilliseconds(100),
                                    Delay(5.Seconds(), Execute(_ => counter.GetAndIncrement())),
                                    Execute(_ =>
                                    {
                                        counter.GetAndDecrement();
                                        Sender.Tell("TIMEOUT");
                                    })),
                                Execute(_ => latch.CountDown())))), null);
            }
        }

        public class WhileCountdown : GT<AtomicCounter>
        {
            public WhileCountdown(AtomicCounter counter)
            {
                StartWith(
                    NextMessage(
                        While(ctx => ctx.Data.Current > 0,
                            ctx =>
                             {
                                 Sender.Tell(ctx.Data.Current);
                                 ctx.Data.GetAndDecrement();
                                 Self.Tell("AGAIN", Sender);
                             })), counter);
            }
        }

        public class TimeoutAllSucceed : GT<object>
        {
            public TimeoutAllSucceed(TestLatch latch)
            {
                StartWith(
                    Timeout(30.Seconds(),
                        AllSucceed(
                            Execute(ctx => latch.CountDown())),
                        Execute(ctx => { throw new TimeoutException(); })), null);
            }
        }

        public class JustDelay : GT<object>
        {
            public JustDelay(TestLatch latch)
            {
                StartWith(
                    Delay(100.Milliseconds(), Execute(ctx => latch.CountDown())), null);
            }
        }

        public class ScheduleTellConstructor : GT<object>
        {
            public ScheduleTellConstructor(IActorRef receiver)
            {
                Context.System.Scheduler.ScheduleTellOnceCancelable(100.Milliseconds(), Self, "HELLO", receiver);

                StartWith(
                    NextMessage(Execute(ctx => Sender.Tell(ctx.CurrentMessage + " FROM ME"))), null);
            }
        }

        public class ScheduleTellExecute : GT<object>
        {
            public ScheduleTellExecute(IActorRef receiver)
            {
                StartWith(
                    AllSucceed(
                        Execute(ctx => Context.System.Scheduler.ScheduleTellOnceCancelable(100.Milliseconds(), Self, "HELLO", receiver)),
                        NextMessage(Execute(ctx => Sender.Tell(ctx.CurrentMessage + " FROM ME")))), null);
            }
        }

        public class BecomeIfReceive : GT<object>, IWithUnboundedStash
        {
            public BecomeIfReceive()
            {
                StartWith(NextMessage(Odd()), null);
            }

            public IStash Stash { get; set; }

            public GoalMachine.IGoal Odd() =>
                Loop(
                    AllComplete(
                        If(ctx => ((int)ctx.CurrentMessage) % 2 == 1,
                            Execute(_ => Sender.Tell("ODD")),
                            Become(Even)),
                        NextMessage()));

            public GoalMachine.IGoal Even() =>
                Loop(
                    AllComplete(
                        If(ctx => ((int)ctx.CurrentMessage) % 2 == 0,
                            Execute(_ => Sender.Tell("EVEN")),
                            Become(Odd)),
                        NextMessage()));
        }

        public class ScheduleTimeout : GT<object>
        {
            private bool _scheduled;
            private IActorRef _client;

            public ScheduleTimeout(bool shortTime)
            {
                var time1 = shortTime ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(1);
                var time2 = TimeSpan.FromMilliseconds(1100) - time1;

                StartWith(AllComplete(
                    Client(),
                    Timeout(time2,
                        Schedule(time1),
                        Execute(_ => _client.Tell("TIMEOUT!")))), null);
            }

            public GoalMachine.IGoal Client() =>
                When(ctx => ctx.CurrentMessage == "START!", Execute(_ => _client = Sender));

            public GoalMachine.IGoal Schedule(TimeSpan delay) =>
                FromAction(ctx =>
                {
                    if (ctx.CurrentMessage == "TIME!")
                    {
                        _client.Tell("IT IS TIME!");
                        ctx.Status = GoalStatus.Success;
                    }
                    else if (!_scheduled)
                    {
                        ctx.ScheduleMessage("TIME!", delay);
                        _scheduled = true;
                    }
                });
        }

        public class ToyTest : GT<object>
        {
            public abstract class Message
            {
                public class ConnectedMsg : Message { }
                public class DisconnectedMsg : Message { }
                public class DoBark : Message { }
                public class DoSleep : Message { }
                public class DoJump : Message { }
                public class DoWake : Message { }
            }

            public ToyTest()
            {
                StartWith(Disconnected(), null);
            }

            GoalMachine.IGoal Disconnected() =>
                NextMessage<Message.ConnectedMsg>(
                    Become(Connected)); // Become takes in a Func, in order avoid cycles in graph

            GoalMachine.IGoal Connected() =>
                Parallel(ss => ss.AllComplete(), // AllComplete irrelevant when Become overwrites the whole workflow
                    NextMessage<Message.DisconnectedMsg>(
                        Become(Disconnected)),
                    Spawn(Awake())); // Using Spawn to limit Become scope, Becomes under Spawn overwrite Spawn child
            // Spawns still belong to their parent, and will be overwritten along with parent
            GoalMachine.IGoal Awake() =>
                AllComplete(
                    Execute(_ => Sender.Tell("YAWN")),
                    Forever( // Forever until Become
                       Parallel(ss => ss.AnySucceed(), // Parallel to listen to several message types/conditions
                           NextMessage<Message.DoBark>(Execute(ctx => Sender.Tell("BARK"))), // Receive is a single callback
                           NextMessage<Message.DoJump>(Execute(ctx => Sender.Tell("WHEE"))), // use loops to receive again
                           NextMessage<Message.DoSleep>(Become(Sleeping)))));

            GoalMachine.IGoal Sleeping() =>
                AllComplete(
                    Execute(_ => Sender.Tell("ZZZZ")),
                    NextMessage<Message.DoWake>( // Ignoring all messages besides DoWake
                        Become(Awake))); // But the Parallel in Connected also has a DisconnectedMsg listener
        }

        public class IfNoElse : GT<object>
        {
            public IfNoElse()
            {
                StartWith(Test(), null);
            }

            GoalMachine.IGoal Test() =>
                NextMessage(
                    If(
                        If(ctx => ctx.CurrentMessage.Equals("TRUE"),
                            Execute(_ => Sender.Tell("SUCCESS"))),
                        @else: Execute(_ => Sender.Tell("FAILURE"))));
        }

        public class ForeverParallelReceiveIfNoElse : GT<object>
        {
            public ForeverParallelReceiveIfNoElse()
            {
                StartWith(Test(), null);
            }

            GoalMachine.IGoal Test() =>
                Forever(
                    Parallel(ss => ss.AnyComplete(),
                        NextMessage<string>(s => s.StartsWith("MEOW"),
                            If(ctx => ctx.CurrentMessage.Equals("MEOWCAT"),
                                Execute(_ => Sender.Tell("UCAT")))),
                        NextMessage<string>(s => s.StartsWith("BARK"),
                            If(ctx => ctx.CurrentMessage.Equals("BARKDOG"),
                                Execute(_ => Sender.Tell("UDOG")))),
                        NextMessage<string>(s => s.StartsWith("BLAH"),
                            If(ctx => ctx.CurrentMessage.Equals("BLAHPAL"),
                                Execute(_ => Sender.Tell("UPAL"))))
                    ));
        }

        public class ReceiveNoChild : GT<object>
        {
            public ReceiveNoChild()
            {
                StartWith(Test(), null);
            }

            GoalMachine.IGoal Test() =>
                NextMessage(
                    AllComplete(
                        Execute(_ => Sender.Tell("ANY")),
                        NextMessage(s => s.Equals("ONE"),
                            AllComplete(
                                Execute(_ => Sender.Tell("ANYONE")),
                                NextMessage<int>(
                                    AllComplete(
                                        Execute(_ => Sender.Tell("THREE")),
                                        NextMessage<int>(i => i % 2 == 0,
                                            Execute(_ => Sender.Tell("THREEEVEN")))))))));
        }

        public class WhenWhenWhen : GT<object>
        {
            public WhenWhenWhen()
            {
                StartWith(Test(), null);
            }

            GoalMachine.IGoal Test() =>
                When(When(GetMessage("START"),
                        Execute(ctx => Sender.Tell("SUCCESS!"))),
                    When(When(ctx => ctx.CurrentMessage == "A"),
                        Execute(_ => Sender.Tell("A!"))));

            GoalMachine.IGoal GetMessage(string msg) =>
                If(ctx => ctx.CurrentMessage == msg,
                    Execute(_ => Sender.Tell($"GOT {msg}!")),
                    Then(Execute(_ => Sender.Tell($"{_.CurrentMessage}, NOT {msg}!")), Fail()));
        }

        public class OnMessageVsNextMessage : GT<object>
        {
            private int _counter;

            public OnMessageVsNextMessage()
            {
                StartWith(
                    Parallel(GoalEx.AllComplete,
                        FromAction(_ => _counter++),
                        AllComplete(
                            Parallel(GoalEx.AllComplete,
                                Next("A", "NEXT"),
                                On("A", "ON")),
                            Parallel(GoalEx.AllComplete,
                                Next("A", "NEXT"),
                                On("A", "ON")))), null);
            }

            GoalMachine.IGoal On(string msg, string reply) =>
                OnMessage<string>(s => s.Equals(msg), Execute(_ => Sender.Tell($"{_counter}: {reply}")));

            GoalMachine.IGoal Next(string msg, string reply) =>
                NextMessage<string>(s => s.Equals(msg), Execute(_ => Sender.Tell($"{_counter}: {reply}")));
        }

        public class FailChild : GT<object>
        {
            public FailChild()
            {
                StartWith(
                    If(
                        Fail(OnMessage<string>(s => s == "START!", Execute(_ => Sender.Tell("MESSAGE!")))),
                        Execute(_ => Sender.Tell("THEN!")),
                        Execute(_ => Sender.Tell("ELSE!"))), null);
            }
        }

        public class SimpleFactory : GT<object>
        {
            public SimpleFactory()
            {
                StartWith(
                    AllSucceed(
                        When(OnMessage<string>(s => s.Equals("START!"),
                            Factory(ctx =>
                            {
                                Sender.Tell("FACTORY STARTING!");
                                return Execute(x => Sender.Tell("FACTORY STARTED!"));
                            })))), null);
            }
        }

        public class ForeverWhenTrueIf : GT<object>
        {
            public ForeverWhenTrueIf()
            {
                StartWith(
                    Forever(When(_ => true,
                        If(ctx => ctx.CurrentMessage == "OK", Execute(_ => Sender.Tell("GO"))))), null);
            }
        }

        public class NotAllSucceed : GT<object>
        {
            public NotAllSucceed()
            {
                StartWith(
                    AllSucceed(
                        NextMessage(),
                        Execute(_ => Sender.Tell("1")),
                        Fail(),
                        Execute(_ => Sender.Tell("3"))), null);
            }
        }

        public class IfElseFail : GT<object>
        {
            public IfElseFail()
            {
                StartWith(
                    NextMessage(
                        If(
                            If(_ => false,
                                Execute(__ => Sender.Tell("In Ok")),
                                Fail(Execute(__ => Sender.Tell("In Fail")))),
                            Execute(_ => Sender.Tell("Out Ok")),
                            Fail(Execute(_ => Sender.Tell("Out Fail"))))), null);
            }
        }

        public class ThenFail : GT<object>
        {
            public ThenFail()
            {
                StartWith(
                    NextMessage(
                        If(
                           Then(
                                Execute(__ => Sender.Tell("Ok So Far")),
                                Fail(Execute(__ => Sender.Tell("Then Fail")))),
                            Execute(_ => Sender.Tell("Out Ok")),
                            Fail(Execute(_ => Sender.Tell("Out Fail"))))), null);
            }
        }

        public class TimeoutThenFail : GT<object>
        {
            public TimeoutThenFail()
            {
                StartWith(
                    NextMessage(
                        If(
                            Timeout(100.Milliseconds(),
                                When(_ => false),
                                Fail(Execute(_ => Sender.Tell("Failed On Timeout")))),
                            Execute(_ => Sender.Tell("Out Ok")),
                            Fail(Execute(_ => Sender.Tell("Out Fail"))))), null);
            }
        }

        public class ExecuteExceptionFailEventActor : GT<object>
        {
            public ExecuteExceptionFailEventActor(List<object> reasons)
            {
                FailureReported += (s, o) => reasons.Add(o);

                StartWith(NextMessage(
                    If(Execute(_ =>
                        {
                            Sender.Tell("RUNNING");
                            throw new Exception("Failed");
                        }),
                        Execute(_ => Sender.Tell("SUCCESS")),
                        Execute(_ => Sender.Tell("FAILURE")))), null);
            }
        }

        public class SequenceBench : GT<object>
        {
            public SequenceBench(
                AtomicCounter goals,
                TestLatch done,
                AtomicBoolean testResult,
                Func<IEnumerable<GoalStatus>, GoalStatus> statusFunc,
                params bool[] results)
            {
                StartWith(
                    AllComplete(
                        If(Sequence(statusFunc,
                            results.Select(r =>
                                AllSucceed(
                                    Execute(_ => goals.IncrementAndGet()),
                                    Condition(_ => r))).OfType<GoalMachine.IGoal>().ToArray()),
                            Execute(_ => testResult.Value = true),
                            Execute(_ => testResult.Value = false)),
                        Execute(_ => done.CountDown())), null);
            }
        }

        public class SequenceBenchRunner
        {
            private GTActorSpec _spec;

            public SequenceBenchRunner(GTActorSpec spec)
            {
                _spec = spec;
            }

            public int GoalsRun;
            public bool Result;

            public void Run(bool expectedResult, int expectedCount, Func<IEnumerable<GoalStatus>, GoalStatus> statusFunc, params bool[] results)
            {
                var done = new TestLatch();
                var goals = new AtomicCounter(0);
                var result = new AtomicBoolean();

                var gt = _spec.Sys.ActorOf(Props.Create(() => new SequenceBench(goals, done, result, statusFunc, results)));

                done.Ready();

                GoalsRun = goals.Current;
                Result = result.Value;

                Assert.Equal(expectedResult, Result);
                Assert.Equal(expectedCount, GoalsRun);
            }
        }

        #endregion

        [Fact]
        public void GTActor_ConstructorIncrement()
        {
            var counter = new AtomicCounter(0);
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new ConstructorIncrement(counter, latch)));

            latch.Ready();
            Assert.Equal(1, counter.Current);
        }

        [Fact]
        public void GTActor_ExecuteIncrement()
        {
            var counter = new AtomicCounter(0);
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new ExecuteIncrement(counter, latch)));

            latch.Ready();
            Assert.Equal(1, counter.Current);
        }

        [Fact]
        public void GTActor_OneEcho_Exactly_One()
        {
            var bt = Sys.ActorOf<OneEcho>();

            bt.Tell("echo1", TestActor);

            ExpectMsg((object)"echo1");

            bt.Tell("echo1", TestActor);

            ExpectNoMsg(100);
        }

        [Fact]
        public void GTActor_NestedEcho_Second()
        {
            var bt = Sys.ActorOf<NestedEcho>();

            bt.Tell("echo1", TestActor);
            bt.Tell("echo2", TestActor);
            ExpectMsg((object)"echo2");

            bt.Tell("echo3", TestActor);

            ExpectNoMsg(100);
        }

        [Fact]
        public void GTActor_SequenceOne()
        {
            var pipe = new List<string>();
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new SequenceOne(pipe, latch)));

            latch.Ready();

            Assert.Equal(new[] { "1" }, pipe);
        }

        [Fact]
        public void GTActor_SequenceTwo()
        {
            var pipe = new List<string>();
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new SequenceTwo(pipe, latch)));

            latch.Ready();

            Assert.Equal(new[] { "1", "2" }, pipe);
        }

        [Fact]
        public void GTActor_SequenceReceive()
        {
            var pipe = new List<string>();
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new SequenceReceive(pipe, latch)));

            bt.Tell("2");

            latch.Ready();

            Assert.Equal(new[] { "1", "2" }, pipe);
        }

        [Fact]
        public void GTActor_SequenceReceiveSequenceReceive()
        {
            var pipe = new List<string>();
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new SequenceReceiveSequenceReceive(pipe, latch)));

            bt.Tell("1");
            bt.Tell("3");
            bt.Tell("4");

            latch.Ready();

            Assert.Equal(new[] { "1", "2", "3", "4" }, pipe);
        }

        [Fact]
        public void GTActor_Loop100()
        {
            var bt = Sys.ActorOf(Props.Create(() => new LoopEcho()));

            foreach (var i in Enumerable.Range(1, 100))
            {
                bt.Tell(i.ToString(), TestActor);
            }

            foreach (var i in Enumerable.Range(1, 100))
            {
                ExpectMsg(i.ToString());
            }
        }

        [Fact]
        public void GTActor_SequenceParallelExecute()
        {
            var counter = new AtomicCounter(0);
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new SequenceParallelExecute(counter, latch)));

            latch.Ready();
            Assert.Equal(5, counter.Current);
        }

        [Fact]
        public void GTActor_SequenceParallelReceive()
        {
            var counter = new AtomicCounter(0);
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new SequenceParallelReceive(counter, latch)));

            bt.Tell(new object());
            bt.Tell(new object());

            latch.Ready();
            Assert.Equal(5, counter.Current);
        }

        [Fact]
        public void GTActor_ParallelLoopReceive()
        {
            var counter = new AtomicCounter(0);
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new ParallelLoopReceive(latch)));

            bt.Tell("A", TestActor);
            bt.Tell("B", TestActor);
            ExpectMsg("1A2A");
            ExpectMsg("1B2B");

            bt.Tell("C");
            ExpectMsg("1C2C");
            bt.Tell("D");
            ExpectMsg("1D2D");
        }

        [Fact]
        public void GTActor_RecoverCurrentMessage()
        {
            var bt = Sys.ActorOf(Props.Create(() => new SequenceReceiveReverseThree()));

            foreach (var i in Enumerable.Range(1, 3))
            {
                bt.Tell(i.ToString(), TestActor);
            }

            foreach (var i in Enumerable.Range(1, 3).Reverse())
            {
                ExpectMsg(i.ToString());
            }
        }

        [Fact]
        public void GTActor_BecomePingPong()
        {
            var pipe = new List<string>();
            var latch = new TestLatch(5);

            var bt = Sys.ActorOf(Props.Create(() => new BecomePingPong(pipe, latch)));

            bt.Tell("RUN", TestActor);
            bt.Tell("PING", TestActor);

            latch.Ready();

            ExpectMsg("DONE");

            Assert.Equal(new[] { "PING", "PONG", "PING", "PONG" }, pipe);
        }

        [Fact]
        public void GTActor_Become_Is_Limited_By_Spawn_Scope()
        {
            var bt = Sys.ActorOf(Props.Create(() => new SpawnConfirm()));

            bt.Tell("THERE?", TestActor);
            ExpectMsg("HERE!");

            bt.Tell("KILL", TestActor);
            ExpectMsg("I TRIED");

            bt.Tell("THERE?", TestActor);

            ExpectMsgAllOf(3.Seconds(), "HERE!", "I ATE HIM");
        }

        [Fact]
        public void GTActor_Become_Crosses_NonSpawn_Scopes()
        {
            var bt = Sys.ActorOf(Props.Create(() => new ParallelBecomer()));

            bt.Tell("THERE?", TestActor);
            ExpectMsg("HERE!", TimeSpan.FromHours(1));

            bt.Tell("KILL", TestActor);
            ExpectMsg("I TRIED", TimeSpan.FromHours(1));

            bt.Tell("THERE?", TestActor);
            ExpectMsg("I ATE HIM", TimeSpan.FromHours(1));

            ExpectNoMsg(100);
        }

        [Fact]
        public void GTActor_Spawn_Resets_To_Original_Workflow()
        {
            var bt = Sys.ActorOf(Props.Create(() => new LoopSpawn()));

            bt.Tell(1, TestActor);
            bt.Tell(1, TestActor);
            bt.Tell(1, TestActor);
            bt.Tell(1, TestActor);

            ExpectMsg("KARU");
            ExpectMsg("SEL");
            ExpectMsg("KARU");
            ExpectMsg("SEL");
        }

        [Fact]
        public void GTActor_All_Conditions()
        {
            var bt = Sys.ActorOf(Props.Create(() => new AllConditions()));

            bt.Tell("GO", TestActor);
            bt.Tell("LEFT", TestActor);
            bt.Tell("RIGHT", TestActor);
            bt.Tell("YES");
            bt.Tell("NO");
            bt.Tell("NO");
            bt.Tell("YES");

            ExpectMsg("I GO");
            ExpectMsg("I LEFT");
            ExpectMsg("I RIGHT");
            bt.Tell("I YES");
            bt.Tell("I NO");
            bt.Tell("I NO");
            bt.Tell("I YES");
        }

        [Fact]
        public void GTActor_FailFails()
        {
            var bt = Sys.ActorOf(Props.Create(() => new FailFails()));

            bt.Tell("SUCCESS", TestActor);
            ExpectMsg("SUCCESS");
        }

        [Fact]
        public void GTActor_NotFailSucceeds()
        {
            var bt = Sys.ActorOf(Props.Create(() => new NotFailSucceeds()));

            bt.Tell("SUCCESS", TestActor);
            ExpectNoMsg(200);
        }

        [Fact]
        public void GTActor_Fail_After_Success_Succeeds()
        {
            TestLatch latch = new TestLatch();
            AtomicCounter counter = new AtomicCounter(0);

            var bt = Sys.ActorOf(Props.Create(() => new FailAfterSuccess(latch, counter)));

            latch.Ready();
            Assert.Equal(2, counter.Current);
        }

        [Fact]
        public void GTActor_Success_After_Failure_Fails()
        {
            TestLatch latch = new TestLatch();
            AtomicCounter counter = new AtomicCounter(0);

            var bt = Sys.ActorOf(Props.Create(() => new SuccessAfterFailure(latch, counter)));

            latch.Ready();
            Assert.Equal(2, counter.Current);
        }

        [Fact]
        public void GTActor_Long_Timeout_Short_Delay()
        {
            TestLatch latch = new TestLatch();
            AtomicCounter counter = new AtomicCounter(0);

            var bt = Sys.ActorOf(Props.Create(() => new LongTimeoutShortTask(latch, counter)));

            bt.Tell("RUN", TestActor);
            latch.Ready();
            latch.Reset();
            bt.Tell("RUN", TestActor);

            latch.Ready();
            Assert.Equal(2, counter.Current);

            ExpectNoMsg(200);
        }

        [Fact]
        public void GTActor_Short_Timeout_Long_Delay()
        {
            TestLatch latch = new TestLatch(2);
            AtomicCounter counter = new AtomicCounter(0);

            var bt = Sys.ActorOf(Props.Create(() => new ShortTimeoutLongTask(latch, counter)));

            bt.Tell("RUN", TestActor);
            ExpectMsg("TIMEOUT");
            bt.Tell("RUN", TestActor);
            ExpectMsg("TIMEOUT");

            latch.Ready();
            Assert.Equal(-2, counter.Current);

            ExpectNoMsg(100);
        }

        [Fact]
        public void GTActor_NextMessage_While()
        {
            AtomicCounter counter = new AtomicCounter(5);

            var bt = Sys.ActorOf(Props.Create(() => new WhileCountdown(counter)));

            bt.Tell("RUN", TestActor);

            Enumerable.Range(1, 5).Reverse().ForEach(i => ExpectMsg(i));
        }

        [Fact]
        public void GTActor_Timeout_Runs_AllSucceed()
        {
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new TimeoutAllSucceed(latch)));

            latch.Ready();
        }

        [Fact]
        public void GTActor_Delay_Simple()
        {
            var latch = new TestLatch();

            var bt = Sys.ActorOf(Props.Create(() => new JustDelay(latch)));

            latch.Ready();
        }

        [Fact]
        public void GTActor_SchedulTell()
        {
            IScheduler scheduler = new HashedWheelTimerScheduler(Sys.Settings.Config, Log);

            var cancellable = scheduler.ScheduleTellOnceCancelable(100.Milliseconds(), TestActor, "Test", ActorRefs.NoSender);

            ExpectMsg("Test");

            scheduler.AsInstanceOf<IDisposable>().Dispose();
        }

        [Fact]
        public void GTActor_Schedule_Tell_In_Constructor()
        {
            var bt = Sys.ActorOf(Props.Create(() => new ScheduleTellConstructor(TestActor)));
            ExpectMsg("HELLO FROM ME");
        }

        [Fact]
        public void GTActor_Schedule_Tell_In_Execute()
        {
            var bt = Sys.ActorOf(Props.Create(() => new ScheduleTellExecute(TestActor)));
            ExpectMsg("HELLO FROM ME");
        }

        [Fact]
        public void GTActor_If_Loop_Receive_Become()
        {
            var bt = Sys.ActorOf(Props.Create(() => new BecomeIfReceive()));

            var toSend = new int[] { 2, 1, 2, 2, 1, 1, 2, 2, 2, 1, 1, 1, 2, 2, 1, 1, 2, 1 };
            var toExpe = toSend.Select(i => i % 2 == 1 ? "ODD" : "EVEN").ToList();

            foreach (var m in toSend)
            {
                bt.Tell(m, TestActor);
            }

            foreach (var m in toExpe)
            {
                ExpectMsg(m, TimeSpan.FromHours(1));
            }
        }

        [Fact]
        public void GTActor_ToyTest()
        {
            var bt = Sys.ActorOf(Props.Create(() => new ToyTest()));

            Sys.EventStream.Subscribe(TestActor, typeof(UnhandledMessage));

            Action<object> assertUnhandled = s =>
            {
                bt.Tell(s, TestActor);
                ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            };

            Action<object, object> assertReply = (s, r) =>
            {
                bt.Tell(s, TestActor);
                ExpectMsg(r);
            };

            assertUnhandled("Hey");
            assertUnhandled(new ToyTest.Message.DoBark());
            assertReply(new ToyTest.Message.ConnectedMsg(), "YAWN");
            assertUnhandled(new ToyTest.Message.ConnectedMsg());
            assertUnhandled(new ToyTest.Message.DoWake());
            assertReply(new ToyTest.Message.DoBark(), "BARK");
            assertReply(new ToyTest.Message.DoJump(), "WHEE");
            assertReply(new ToyTest.Message.DoSleep(), "ZZZZ");
            assertUnhandled(new ToyTest.Message.DoBark());
            assertReply(new ToyTest.Message.DoWake(), "YAWN");
            assertReply(new ToyTest.Message.DoBark(), "BARK");

            bt.Tell(new ToyTest.Message.DisconnectedMsg(), TestActor);
            assertUnhandled(new ToyTest.Message.DoBark());
            assertReply(new ToyTest.Message.ConnectedMsg(), "YAWN");
        }

        [Fact]
        public void GTActor_IfNoElse_False_Fails()
        {
            var bt = Sys.ActorOf(Props.Create(() => new IfNoElse()));

            bt.Tell("FALSE");

            ExpectMsg("FAILURE", TimeSpan.FromHours(1));
        }

        [Fact]
        public void GTActor_Parallel_IfNoElse_Bug()
        {
            var bt = Sys.ActorOf(Props.Create(() => new ForeverParallelReceiveIfNoElse()));

            Action<object, object> assertReply = (s, r) =>
            {
                bt.Tell(s, TestActor);
                ExpectMsg(r, 5.Minutes());
            };

            assertReply("MEOWCAT", "UCAT");
            bt.Tell("BARKPAL", TestActor);
            assertReply("BARKDOG", "UDOG");
        }

        [Fact]
        public void GTActor_ReceiveNoChild_Only_Succeeds()
        {
            var bt = Sys.ActorOf(Props.Create(() => new ReceiveNoChild()));

            Sys.EventStream.Subscribe(TestActor, typeof(UnhandledMessage));

            Action<object> assertUnhandled = s =>
            {
                bt.Tell(s, TestActor);
                ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            };

            Action<object, object> assertReply = (s, r) =>
            {
                bt.Tell(s, TestActor);
                ExpectMsg(r);
            };

            assertReply(new object(), "ANY");
            assertUnhandled("TWO");
            assertReply("ONE", "ANYONE");
            assertUnhandled("STR");
            assertReply(5, "THREE");
            assertUnhandled(3);
            assertReply(4, "THREEEVEN");

            Sys.EventStream.Unsubscribe(TestActor, typeof(UnhandledMessage));
        }

        [Fact]
        public void GTActor_When1()
        {
            var gt = Sys.ActorOf(Props.Create(() => new WhenWhenWhen()));

            Action<object> assertUnhandled = s =>
            {
                gt.Tell(s, TestActor);
                ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            };

            Action<object, object> assertReply = (s, r) =>
            {
                gt.Tell(s, TestActor);
                ExpectMsg(r);
            };

            assertReply("A", "A, NOT START!");
            assertReply("B", "B, NOT START!");
            assertReply("START", "GOT START!");
            ExpectMsg("SUCCESS!");

            assertUnhandled("B");
            assertReply("A", "A!");
        }

        [Fact]
        public void GTActor_ScheduleTimeoutShort()
        {
            var gt = Sys.ActorOf(Props.Create(() => new ScheduleTimeout(true)));

            Action<object, object> assertReply = (s, r) =>
            {
                gt.Tell(s, TestActor);
                ExpectMsg(r);
            };

            Action<object> assertUnhandled = s =>
            {
                gt.Tell(s, TestActor);
                ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            };

            assertUnhandled("GO!");
            assertReply("START!", "IT IS TIME!");
        }

        [Fact]
        public void GTActor_ScheduleTimeoutLong()
        {
            var gt = Sys.ActorOf(Props.Create(() => new ScheduleTimeout(false)));

            Action<object, object> assertReply = (s, r) =>
            {
                gt.Tell(s, TestActor);
                ExpectMsg(r, TimeSpan.FromHours(1));
            };

            Action<object> assertUnhandled = s =>
            {
                gt.Tell(s, TestActor);
                ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            };

            assertUnhandled("GO!");
            assertReply("START!", "TIMEOUT!");
        }

        [Fact]
        public void GTActor_OnChecksNowNextChecksNext()
        {
            var gt = Sys.ActorOf(Props.Create(() => new OnMessageVsNextMessage()));

            Action<object, object> assertReply = (s, r) =>
            {
                gt.Tell(s, TestActor);
                ExpectMsg(r, TimeSpan.FromHours(1));
            };

            gt.Tell("A", TestActor);
            ExpectMsg("2: NEXT");
            ExpectMsg("2: ON");
            ExpectMsg("2: ON");
            gt.Tell("A", TestActor);
            ExpectMsg("3: NEXT");
        }

        [Fact]
        public void GTActor_FailChild()
        {
            var gt = Sys.ActorOf(Props.Create(() => new FailChild()));

            gt.Tell("START!", TestActor);
            ExpectMsg("MESSAGE!");
            ExpectMsg("ELSE!");
        }

        [Fact]
        public void GTActor_SimpleFactory()
        {
            var gt = Sys.ActorOf(Props.Create(() => new SimpleFactory()));

            gt.Tell("START!", TestActor);
            ExpectMsg("FACTORY STARTING!");
            ExpectMsg("FACTORY STARTED!");
        }

        [Fact]
        public void GTActor_ForeverAvoidsInfiniteLoop()
        {
            var gt = Sys.ActorOf(Props.Create(() => new ForeverWhenTrueIf()));

            gt.Tell("KO", TestActor);
            gt.Tell("OK", TestActor);
            ExpectMsg("GO");
        }

        [Fact]
        public void GTActor_AllSucceedCutsOnFailure()
        {
            var gt = Sys.ActorOf(Props.Create(() => new NotAllSucceed()));

            gt.Tell("0", TestActor);
            ExpectMsg("1");
            ExpectNoMsg(100);
        }

        [Fact]
        public void GTActor_IfInsideElseFailsOutsideIfFails()
        {
            var gt = Sys.ActorOf(Props.Create(() => new IfElseFail()));

            gt.Tell("0", TestActor);
            ExpectMsg("In Fail");
            ExpectMsg("Out Fail");
        }

        [Fact]
        public void GTActor_ThenFailAllFail()
        {
            var gt = Sys.ActorOf(Props.Create(() => new ThenFail()));

            gt.Tell("0", TestActor);
            ExpectMsg("Ok So Far");
            ExpectMsg("Then Fail");
            ExpectMsg("Out Fail");
        }

        [Fact]
        public void GTActor_TimeoutThenFailAllFail()
        {
            var gt = Sys.ActorOf(Props.Create(() => new TimeoutThenFail()));

            gt.Tell("0", TestActor);
            ExpectMsg("Failed On Timeout");
            ExpectMsg("Out Fail");
        }

        [Fact]
        public void GTActor_Execute_Exception_Reports_Failure()
        {
            List<object> reasons = new List<object>();

            var gt = Sys.ActorOf(Props.Create(() => new ExecuteExceptionFailEventActor(reasons)));

            gt.Tell("START", TestActor);

            ExpectMsg("RUNNING");
            ExpectMsg("FAILURE");
            Assert.Equal(1, reasons.Count);
            Assert.IsType<Exception>(reasons[0]);
        }

        [Fact]
        public void GTActor_Sequence_Verify_AllSucceedThru()
        {
            var bench = new SequenceBenchRunner(this);

            bench.Run(false, 0, GoalEx.AllSucceedThru);

            bench.Run(true, 1, GoalEx.AllSucceedThru, true);

            bench.Run(false, 1, GoalEx.AllSucceedThru, false);

            bench.Run(true, 2, GoalEx.AllSucceedThru, true, true);

            bench.Run(false, 2, GoalEx.AllSucceedThru, true, false);

            bench.Run(false, 2, GoalEx.AllSucceedThru, false, true);

            bench.Run(false, 2, GoalEx.AllSucceedThru, false, false);
        }

        [Fact]
        public void GTActor_Sequence_Verify_AnySucceedThru()
        {
            var bench = new SequenceBenchRunner(this);

            bench.Run(false, 0, GoalEx.AnySucceedThru);

            bench.Run(true, 1, GoalEx.AnySucceedThru, true);

            bench.Run(false, 1, GoalEx.AnySucceedThru, false);

            bench.Run(true, 2, GoalEx.AnySucceedThru, true, true);

            bench.Run(true, 2, GoalEx.AnySucceedThru, true, false);

            bench.Run(true, 2, GoalEx.AnySucceedThru, false, true);

            bench.Run(false, 2, GoalEx.AnySucceedThru, false, false);
        }
    }
}