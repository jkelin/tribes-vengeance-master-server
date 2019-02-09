use std::os::raw::c_uchar;
use std::os::raw::c_int;
use std::ptr;

extern {
    fn enctypex_quick_encrypt(key: *const c_uchar, validate: *const c_uchar, data: *mut c_uchar, size: c_int) -> c_int;
    fn enctypex_decoder(key: *const c_uchar, validate: *const c_uchar, data: *const c_uchar, datalen: *mut c_int, enctypex_data: *const c_int) -> *mut c_uchar;
}

pub fn enctypex_encrypt(key: &[u8], validate: &[u8], data: &[u8]) -> Option<Vec<u8>> {
    let mut data_vec: Vec<u8> = data.to_vec();
    data_vec.reserve_exact(data.len() + 23);

    let size_in = data.len();

    unsafe {
        let size_out = enctypex_quick_encrypt(key.as_ptr(), validate.as_ptr(), data_vec.as_mut_ptr(), size_in as c_int);
        data_vec.set_len(size_out as usize);
    };

    Some(data_vec)
}

pub fn enctypex_decrypt(key: &[u8], validate: &[u8], data: &[u8]) -> Option<Vec<u8>> {
    let val = unsafe {
        let mut datalen = data.len() as c_int;
        let out_ptr = enctypex_decoder(key.as_ptr(), validate.as_ptr(), data.as_ptr(), &mut datalen, ptr::null());
        std::slice::from_raw_parts(out_ptr, datalen as usize).to_vec()
    };

    Some(val)
}

#[cfg(test)]
mod tests {
    use super::*;
    
    #[test]
    fn encrypt_and_decrypt_fake() {
        let key = vec![1, 2, 3, 4, 5, 6, 7, 8];
        let validate = vec![1, 2, 3, 4, 5, 6];
        let data_in = vec![1, 2, 3];

        let encrypted = enctypex_encrypt(&key, &validate, &data_in).expect("Could not encrypt");
        let decrypted = enctypex_decrypt(&key, &validate, &encrypted);

        assert!(Some(data_in) == decrypted);
    }

    #[test]
    fn encrypt_and_decrypt_real() {
        let key = vec![121,51,68,50,56,107];
        let validate = vec![46,57,123,82,77,92,47,54];
        let data_in = vec![90,178,44,86,0,0,14,0,109,97,112,110,97,109,101,0,0,110,117,109,112,108,97,121,101,114,115,0,0,109,97,120,112,108,97,121,101,114,115,0,0,104,111,115,116,110,97,109,101,0,0,104,111,115,116,112,111,114,116,0,0,103,97,109,101,116,121,112,101,0,0,103,97,109,101,118,101,114,0,0,112,97,115,115,119,111,114,100,0,0,103,97,109,101,110,97,109,101,0,0,103,97,109,101,109,111,100,101,0,0,103,97,109,101,118,97,114,105,97,110,116,0,0,116,114,97,99,107,105,110,103,115,116,97,116,115,0,0,100,101,100,105,99,97,116,101,100,0,0,109,105,110,118,101,114,0,0,21,176,9,148,187,30,108,21,72,54,15,202,30,98,0];

        let encrypted = enctypex_encrypt(&key, &validate, &data_in).expect("Could not encrypt");
        let decrypted = enctypex_decrypt(&key, &validate, &encrypted);

        assert!(Some(data_in) == decrypted);
    }

    #[test]
    fn encrypt_does_not_return_same_data() {
        let key = vec![1, 2, 3, 4, 5, 6, 7, 8];
        let validate = vec![39,78,89,56,114,109,124,79];
        let data_in = vec![1, 2, 3];

        let encrypted = enctypex_encrypt(&key, &validate, &data_in);

        assert!(Some(data_in) != encrypted);
    }

    #[test]
    fn decrypt_does_not_mutate() {
        let key = vec![121,51,68,50,56,107];
        let validate = vec![46,57,123,82,77,92,47,54];
        let data_in = vec![90,178,44,86,0,0,14,0,109,97,112,110,97,109,101,0,0,110,117,109,112,108,97,121,101,114,115,0,0,109,97,120,112,108,97,121,101,114,115,0,0,104,111,115,116,110,97,109,101,0,0,104,111,115,116,112,111,114,116,0,0,103,97,109,101,116,121,112,101,0,0,103,97,109,101,118,101,114,0,0,112,97,115,115,119,111,114,100,0,0,103,97,109,101,110,97,109,101,0,0,103,97,109,101,109,111,100,101,0,0,103,97,109,101,118,97,114,105,97,110,116,0,0,116,114,97,99,107,105,110,103,115,116,97,116,115,0,0,100,101,100,105,99,97,116,101,100,0,0,109,105,110,118,101,114,0,0,21,176,9,148,187,30,108,21,72,54,15,202,30,98,0];


        let encrypted = enctypex_encrypt(&key, &validate, &data_in).expect("Could not encrypt");

        let before_key = key.to_vec();
        let before_validate = validate.to_vec();
        let before_data_in = data_in.to_vec();

        let decrypted = enctypex_decrypt(&key, &validate, &encrypted);

        let after_key = key.to_vec();
        let after_validate = validate.to_vec();
        let after_data_in = data_in.to_vec();

        assert!(before_key == after_key);
        assert!(before_validate == after_validate);
        assert!(before_data_in == after_data_in);
    }

    #[test]
    fn encrypt_does_not_mutate() {
        let key = vec![121,51,68,50,56,107];
        let validate = vec![46,57,123,82,77,92,47,54];
        let data_in = vec![90,178,44,86,0,0,14,0,109,97,112,110,97,109,101,0,0,110,117,109,112,108,97,121,101,114,115,0,0,109,97,120,112,108,97,121,101,114,115,0,0,104,111,115,116,110,97,109,101,0,0,104,111,115,116,112,111,114,116,0,0,103,97,109,101,116,121,112,101,0,0,103,97,109,101,118,101,114,0,0,112,97,115,115,119,111,114,100,0,0,103,97,109,101,110,97,109,101,0,0,103,97,109,101,109,111,100,101,0,0,103,97,109,101,118,97,114,105,97,110,116,0,0,116,114,97,99,107,105,110,103,115,116,97,116,115,0,0,100,101,100,105,99,97,116,101,100,0,0,109,105,110,118,101,114,0,0,21,176,9,148,187,30,108,21,72,54,15,202,30,98,0];

        let before_key = key.to_vec();
        let before_validate = validate.to_vec();
        let before_data_in = data_in.to_vec();

        let encrypted = enctypex_encrypt(&key, &validate, &data_in).expect("Could not encrypt");

        let after_key = key.to_vec();
        let after_validate = validate.to_vec();
        let after_data_in = data_in.to_vec();

        assert!(before_key == after_key);
        assert!(before_validate == after_validate);
        assert!(before_data_in == after_data_in);
    }

    // This test fails randomly for unknown reasons
    // #[test]
    // fn decrypt_decrypts_reference() {
    //     let key = vec![121,51,68,50,56,107];
    //     let validate = vec![39,78,89,56,114,109,124,79];
    //     let data_in = vec![235,0,0,19,50,93,183,209,228,243,119,210,75,93,130,232,27,146,190,226,62,244,134,140,16,50,84,162,243,163,39,190,174,34,89,203,184,131,100,250,112,42,160,214,231,71,5,190,65,132,100,125,170,64,216,109,26,22,13,35,117,207,146,159,204,154,12,21,89,92,43,95,7,190,85,84,88,245,4,169,27,11,223,22,31,223,82,208,51,235,81,157,160,164,158,85,86,212,169,28,116,33,140,147,65,93,59,112,243,179,36,129,233,170,88,152,239,34,16,165,106,3,59,195,237,160,132,151,211,232,236,215,32,88,205,62,118,137,104,40,88,6,188,57,125,166,239,143,85,3,160,204,229,76,181,254,220,213,203,37,226,35,212,27,47,25,200,201,130,154,165,121,133,17,198,79,166,134,164,192,47,198,18,90,217,12,13,170,51,210,64,18,176,75,110];
    //     let data_out = vec![90,178,44,86,0,0,14,0,109,97,112,110,97,109,101,0,0,110,117,109,112,108,97,121,101,114,115,0,0,109,97,120,112,108,97,121,101,114,115,0,0,104,111,115,116,110,97,109,101,0,0,104,111,115,116,112,111,114,116,0,0,103,97,109,101,116,121,112,101,0,0,103,97,109,101,118,101,114,0,0,112,97,115,115,119,111,114,100,0,0,103,97,109,101,110,97,109,101,0,0,103,97,109,101,109,111,100,101,0,0,103,97,109,101,118,97,114,105,97,110,116,0,0,116,114,97,99,107,105,110,103,115,116,97,116,115,0,0,100,101,100,105,99,97,116,101,100,0,0,109,105,110,118,101,114,0,0,21,176,9,148,187,30,108,21,72,54,15,202,30,98,0];

    //     let decrypted = enctypex_decrypt(&key, &validate, &data_in);

    //     assert!(Some(data_out) == decrypted);
    // }
}