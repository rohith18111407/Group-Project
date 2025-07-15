export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  success: boolean;
  message: string;
  data: {
    jwtToken: string;
    refreshToken: string;
    role: string;
    jwtExpiryTime: string;
  };
  errors: any;
}

export interface RegisterRequest {
  username: string;
  password: string;
  roles: string[];
}
