extern crate cc;

fn main() {
    cc::Build::new()
        .file("src/encrtypex_decoder.c")
        .warnings(false) // TODO make sure that there are no warnings
        .compile("encrtypex_decoder");
}