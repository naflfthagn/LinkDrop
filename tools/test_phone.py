import socket, ssl, json, struct, sys, time, os, hashlib, random

HOST = "127.0.0.1"
PORT = 48622
DEVICE_ID = "fedcba9876543210"
DEVICE_NAME = "Pixel 9 Pro · 测试手机"
MAGIC_M = 0x01
MAGIC_B = 0x02

def log(path, text):
    with open(path, "a", encoding="utf-8") as f:
        f.write(text + "\n")

def connect():
    raw = socket.create_connection((HOST, PORT), timeout=15)
    raw.settimeout(90)
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    return ctx.wrap_socket(raw, server_hostname="linkdrop.local")

def send_json(s, obj):
    data = json.dumps(obj, ensure_ascii=False).encode("utf-8")
    s.sendall(struct.pack(">I", len(data) + 1) + bytes([MAGIC_M]) + data)

def recv_exact(s, n):
    buf = b""
    while len(buf) < n:
        chunk = s.recv(n - len(buf))
        if not chunk:
            raise EOFError("socket closed")
        buf += chunk
    return buf

def recv_frame(s):
    n = struct.unpack(">I", recv_exact(s, 4))[0]
    payload = recv_exact(s, n)
    kind = payload[0]
    if kind == MAGIC_M:
        return ("json", json.loads(payload[1:].decode("utf-8")))
    if kind == MAGIC_B:
        tid = struct.unpack(">q", payload[1:9])[0]
        ln = struct.unpack(">I", payload[9:13])[0]
        return ("bin", tid, payload[13:13+ln])
    raise RuntimeError("unknown frame")

def recv_until(s, ops):
    while True:
        f = recv_frame(s)
        if f[0] == "json" and f[1].get("op") in ops:
            return f[1]
        if f[0] == "json":
            print("skipping", f[1], flush=True)

def do_pair(token_path, log_path):
    s = connect()
    send_json(s, {"op":"pair_request","v":1,"deviceId":DEVICE_ID,"deviceName":DEVICE_NAME,"platform":"android"})
    log(log_path, "connected; waiting for PC approval")
    result = recv_until(s, {"pair_result"})
    log(log_path, json.dumps(result, ensure_ascii=False))
    if result.get("ok"):
        with open(token_path, "w", encoding="utf-8") as f:
            f.write(result.get("token", ""))
        log(log_path, "PAIR_OK")
    else:
        log(log_path, "PAIR_FAIL: " + str(result.get("error")))
    s.close()

def send_file(s, tid, path, display_name, log_path):
    size = os.path.getsize(path)
    send_json(s, {"op":"offer","id":str(tid),"name":display_name,"size":size,"mime":"application/octet-stream"})
    reply = recv_until(s, {"accept","reject"})
    if reply.get("op") == "reject":
        raise RuntimeError("offer rejected: " + str(reply.get("reason")))
    h = hashlib.sha256()
    sent = 0
    with open(path, "rb") as f:
        while True:
            chunk = f.read(262144)
            if not chunk:
                break
            h.update(chunk)
            payload = struct.pack(">q", tid) + struct.pack(">I", len(chunk)) + chunk
            s.sendall(struct.pack(">I", 1 + len(payload)) + bytes([MAGIC_B]) + payload)
            sent += len(chunk)
    send_json(s, {"op":"done","id":str(tid),"sha256":h.hexdigest()})
    result = recv_until(s, {"complete","fail"})
    log(log_path, "sent %s: %s" % (display_name, result.get("op")))
    if result.get("op") != "complete":
        raise RuntimeError("transfer failed: " + str(result.get("message")))

def do_send(token_path, log_path, hold, files):
    with open(token_path, "r", encoding="utf-8") as f:
        token = f.read().strip()
    s = connect()
    send_json(s, {"op":"hello","v":1,"deviceId":DEVICE_ID,"deviceName":DEVICE_NAME,"platform":"android","token":token})
    welcome = recv_until(s, {"welcome","error"})
    log(log_path, json.dumps(welcome, ensure_ascii=False))
    if welcome.get("op") != "welcome":
        raise RuntimeError("auth failed: " + str(welcome.get("message")))
    for path, display in files:
        tid = random.randrange(1, 2**62)
        send_file(s, tid, path, display, log_path)
    log(log_path, "ALL_SENT")
    if hold:
        log(log_path, "holding connection")
        time.sleep(hold)
    s.close()

def main():
    mode = sys.argv[1]
    token_path = sys.argv[2]
    log_path = sys.argv[3]
    try:
        if mode == "pair":
            do_pair(token_path, log_path)
        elif mode == "send":
            hold = int(sys.argv[4])
            raw_files = sys.argv[5:]
            files = []
            for item in raw_files:
                path, name = item.split("|", 1)
                files.append((path, name))
            do_send(token_path, log_path, hold, files)
        else:
            raise RuntimeError("unknown mode")
    except Exception as e:
        log(log_path, "EXCEPTION: " + repr(e))
        raise

if __name__ == "__main__":
    main()
