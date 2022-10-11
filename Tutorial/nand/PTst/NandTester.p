
type tReadTestRequest = (blockAddress: int, pageAddress: int, byteAddress: int, len: int);
type tReadTestResponse = (buffer: seq[int], len: int);
type tWriteTestRequest = (blockAddress: int, pageAddress: int, byteAddress: int, buffer: seq[int], len: int);
type tWriteTestResponse = int;

event eReadTestRequest: tReadTestRequest;
event eReadTestResponse: tReadTestResponse;
event eWriteTestRequest: tWriteTestRequest;
event eWriteTestResponse: tWriteTestResponse;

type tNandTesterInit = machine;

machine NandTester
{
    var client: machine;
    var driver: machine;
    var reading: bool;
    var blockAddress: int;
    var pageAddress: int;
    var byteAddress: int;
    var writeBuffer: seq[int];
    var bytesWritten: int;
    var bytes: int;

    fun sendFailure() {
        var readResp: tReadTestResponse;
        var writeResp: tWriteTestResponse;
        var emptyBuff: seq[int];

        if (reading) {
            readResp = (buffer=emptyBuff, len=-1);
            send client, eReadTestResponse, readResp;
        } else {
            send client, eWriteTestResponse, -1;
        }
    }
            
    start state Init {
        entry (dr: tNandTesterInit) {
            driver = dr;
        }
        on eRegisterClient do (clientRef: tRegisterClient) {
            client = clientRef;
            goto testloop;
        }
    }

    state testloop {
        on eReadTestRequest do (req: tReadTestRequest) {
            reading = true;
            blockAddress = req.blockAddress;
            pageAddress = req.pageAddress;
            byteAddress = req.byteAddress;
            bytes = req.len;
            goto sendAddress;
        }

        on eWriteTestRequest do (req: tWriteTestRequest) {
            reading = false;
            blockAddress = req.blockAddress;
            pageAddress = req.pageAddress;
            byteAddress = req.byteAddress;
            writeBuffer = req.buffer;
            bytes = req.len;
            goto sendAddress;
        }
    }

    state sendAddress {
        entry {
            var setReq: tSetNandRegister;
            var waitReq: tWait;
            setReq = (offset=reg_address, val=blockAddress);
            send driver, eSetNandRegister, setReq;
    
            if (reading) {
                setReq = (offset=reg_command, val=1);
            } else {
                setReq = (offset=reg_command, val=3);
            }

            send driver, eSetNandRegister, setReq;
    
            waitReq = 0;
            send driver, eWait, waitReq;
    
            goto awaitingBlockSetDone;
        }
    }

    state awaitingBlockSetDone {
        on eWaitResp do (resp: tWaitResp) {
            var setReq: tSetNandRegister;
            var waitReq: tWait;
            
            if (resp == -1) {
                sendFailure();
                goto testloop;
            }

            setReq = (offset=reg_address, val=pageAddress);
            send driver, eSetNandRegister, setReq;

            waitReq = 0;
            send driver, eWait, waitReq;

            goto awaitingPageSetDone;
        }
    }

    state awaitingPageSetDone {
        on eWaitResp do (resp: tWaitResp) {
            var setReq: tSetNandRegister;
            var waitReq: tWait;
            
            if (resp == -1) {
                sendFailure();
                goto testloop;
            }

            setReq = (offset=reg_address, val=byteAddress);
            send driver, eSetNandRegister, setReq;

            if (reading) {
                setReq = (offset=reg_command, val=2);
            } else {
                setReq = (offset=reg_command, val=4);
            }

            waitReq = 0;
            send driver, eWait, waitReq;

            goto awaitingByteSetDone;
        }
    }

    state awaitingByteSetDone {
        on eWaitResp do (resp: tWaitResp) {
            var writeReq: tProgram;
            
            if (resp == -1) {
                sendFailure();
                goto testloop;
            }

            if (reading) {
                send driver, eRead, bytes;
            } else {
                writeReq = (buffer=writeBuffer, len=bytes);
                send driver, eProgram, writeReq;
            }
            goto awaitingResp;
        }
    }

    state awaitingResp {
        on eReadResp do (resp: tReadResp) {
            var clientResp: tReadTestResponse;
            clientResp = (buffer=resp.buffer, len=resp.len);
            send client, eReadTestResponse, clientResp;
            goto testloop;
        }

        on eProgramResp do (resp: tProgramResp) {
            send client, eWriteTestResponse, resp;
            goto testloop;
        }
    }
}
