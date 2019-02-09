#[macro_use]
extern crate futures;
extern crate tokio;
extern crate encrtypex_decoder;

use std::{env, io};
use std::net::SocketAddr;

use tokio::prelude::*;
use tokio::net::UdpSocket;

struct BeaconServer {
    socket: UdpSocket,
    buf: Vec<u8>,
    to_send: Option<(usize, SocketAddr)>,
}

impl Future for BeaconServer {
    type Item = ();
    type Error = io::Error;

    fn poll(&mut self) -> Poll<(), io::Error> {
        loop {
            println!("poll");
            // First we check to see if there's a message we need to echo back.
            // If so then we try to send it back to the original source, waiting
            // until it's writable and we're able to do so.
            if let Some((size, peer)) = self.to_send {
                let amt = try_ready!(self.socket.poll_send_to(&self.buf[..size], &peer));
                println!("Echoed {}/{} bytes to {}", amt, size, peer);
                self.to_send = None;
            }

            // If we're here then `to_send` is `None`, so we take a look for the
            // next message we're going to echo back.
            self.to_send = Some(try_ready!(self.socket.poll_recv_from(&mut self.buf)));
        }
    }
}


fn main() -> Result<(), Box<std::error::Error>> {
    let addr = env::args().nth(1).unwrap_or("127.0.0.1:8080".to_string());
    let addr = addr.parse::<SocketAddr>()?;

    let socket = UdpSocket::bind(&addr)?;
    println!("Listening on: {}", socket.local_addr()?);

    let server = BeaconServer {
        socket: socket,
        buf: vec![0; 1024],
        to_send: None,
    };

    // This starts the server task.
    //
    // `map_err` handles the error by logging it and maps the future to a type
    // that can be spawned.
    //
    // `tokio::run` spawns the task on the Tokio runtime and starts running.
    tokio::run(server.map_err(|e| println!("server error = {:?}", e)));
    Ok(())
}

// extern crate actix;
// extern crate tokio;
// extern crate tokio_io;
// extern crate futures;
// extern crate bytes;

// use bytes::BytesMut;
// use std::io;
// use std::net::SocketAddr;
// use futures::{Stream, Sink};
// use futures::stream::SplitSink;
// use tokio::net::{UdpSocket, UdpFramed};
// use tokio_io::codec::BytesCodec;
// use actix::prelude::*;
// use actix::{Actor, Context, StreamHandler};

// #[derive(Message)]
// struct UdpPacket(SocketAddr, BytesMut);


// struct UdpActor {
//     sink: SplitSink<UdpFramed<BytesCodec>>
// }

// impl Actor for UdpActor {
//     type Context = Context<Self>;
// }

// impl StreamHandler<UdpPacket, io::Error> for UdpActor {
//     fn handle(&mut self, msg: UdpPacket, _: &mut Context<Self>) {
//         println!("Received: ({:?}, {:?})", msg.0, msg.1);
//         self.sink = self.sink.send(("PING".into(), msg.0))
//     }
// }


// fn main() -> Result<(), Box<std::error::Error>> {
//     let sys = actix::System::new("echo-udp");
//     let addr: SocketAddr = "127.0.0.1:8080".parse()?;
//     let sock = UdpSocket::bind(&addr)?;
//     let (sink, stream) = UdpFramed::new(sock, BytesCodec::new()).split();
//     let _ = UdpActor::create(|ctx| {
//         ctx.add_stream(stream.map(|(data, sender)| UdpPacket(sender, data)));

//         UdpActor{sink: sink}
//     });

//     std::process::exit(sys.run());
// }