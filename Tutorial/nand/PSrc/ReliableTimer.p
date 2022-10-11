/*****************************************************************************************
The timer state machine models the non-deterministic behavior of an OS timer
******************************************************************************************/
machine ReliableTimer
{
    // user of the timer
	var client: machine;
    var running: bool;
	start state Init {
		entry (_client : machine){
			client = _client;
            running = false;
			goto WaitForTimerRequests;
		}
	}

	state WaitForTimerRequests {

		on eStartReliableTimer do {
            running = true;
            send client, eReliableTimerStarted;
            send this, eContinueReliableTimer;
        }
        on eContinueReliableTimer do {
            if (running) {
                if($) {
                    running = false;
                    send client, eReliableTimeOut; 
                } else {
                    send this, eContinueReliableTimer;
                }
            }
        }
		on eCancelReliableTimer do {
            running = false;
		}
	}
}

/************************************************
Events used to interact with the timer machine
************************************************/
event eStartReliableTimer;
event eResetReliableTimer;
event eCancelReliableTimer;
event eCancelReliableTimerFailed;
event eCancelReliableTimerSuccess;
event eReliableTimeOut;
event eContinueReliableTimer;
event eReliableTimerStarted;
/************************************************
Functions or API's to interact with the OS Timer
*************************************************/
// create timer
fun CreateReliableTimer(client: machine) : ReliableTimer
{
	return new ReliableTimer(client);
}

// start timer
fun StartReliableTimer(timer: ReliableTimer)
{
	send timer, eStartReliableTimer;
}

// cancel timer
fun CancelReliableTimer(timer: ReliableTimer)
{
	send timer, eCancelReliableTimer;
	// wait for cancel response, nothing different is done if cancel failed or succeeded.
	receive {
		case eCancelReliableTimerSuccess: { print "Timer Cancelled Successful"; }
		case eCancelReliableTimerFailed: { print "Timer Cancel Failed!"; }
	}
}
