import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { LoginRequest, LoginResponse, RegisterRequest } from "../models/auth.model";
import { Observable, tap } from "rxjs";
import { Router } from "@angular/router";

@Injectable({ providedIn: 'root' })
export class AuthService {
  private baseUrl = 'http://localhost:5239/api/v1/Auth';
  private refreshTimer: any;

  constructor(private http: HttpClient, private router: Router) { }

  login(data: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/Login`, data).pipe(
      tap((response: any) => {
        const { jwtToken, refreshToken, role, jwtExpiryTime } = response.data;
        this.storeTokens(jwtToken, refreshToken, role, jwtExpiryTime);
        this.setupTokenMonitor();
      })
    );
  }

  register(data: RegisterRequest): Observable<any> {
    return this.http.post(`${this.baseUrl}/Register`, data);
  }

  storeTokens(jwtToken: string, refreshToken: string, role: string, jwtExpiryTime: string): void {
    sessionStorage.setItem('jwtToken', jwtToken);
    sessionStorage.setItem('refreshToken', refreshToken);
    sessionStorage.setItem('role', role);
    sessionStorage.setItem('jwtExpiryTime', jwtExpiryTime);
  }

  getRoles(): string[] {
    const role = sessionStorage.getItem('role');
    return role ? [role] : [];
  }

  getUserRole(): string | null {
    const role = sessionStorage.getItem('role');
    return role || null;
  }

  getAccessToken(): string | null {
    return sessionStorage.getItem('jwtToken');
  }

  getRefreshToken(): string | null {
    return sessionStorage.getItem('refreshToken');
  }

  getJwtExpiry(): string | null {
    return sessionStorage.getItem('jwtExpiryTime');
  }

  logout(): void {
    sessionStorage.clear();
  }

  isLoggedIn(): boolean {
    return !!this.getAccessToken();
  }

  setupTokenMonitor(): void {
    clearTimeout(this.refreshTimer);
    const expiry = this.getJwtExpiry();
    if (!expiry) return;

    const expiryTime = new Date(expiry).getTime();
    const now = new Date().getTime();
    const diff = expiryTime - now - 30000; // 30 seconds before expiry

    console.log('JWT Expiry:', expiry);
    console.log('Now:', new Date());
    console.log('Time until prompt (ms):', diff);

    if (diff > 0) {
      this.refreshTimer = setTimeout(() => {
        this.promptExtendSession();
      }, diff);
    } else {
      console.warn('Token already expired or too close to expiry for monitoring.');
    }
  }


  private promptExtendSession(): void {
    const extend = confirm('Your session is about to expire. Do you want to extend it?');
    if (extend) {
      this.refreshToken();
    } else {
      this.logout();
      this.router.navigate(['/login']);
    }
  }

  private refreshToken(): void {
    const body = {
      accessToken: this.getAccessToken(),
      refreshToken: this.getRefreshToken()
    };

    this.http.post<any>(`${this.baseUrl}/refresh`, body).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          const { jwtToken, refreshToken, role, jwtExpiryTime } = response.data;
          this.storeTokens(jwtToken, refreshToken, role, jwtExpiryTime);
          this.setupTokenMonitor();

          const rolePath = role.toLowerCase() === 'admin' ? 'admin' : 'staff';
          this.router.navigate([`/${rolePath}/dashboard`]);
        }
      },
      error: () => {
        alert('Session refresh failed. Please log in again.');
        this.logout();
        this.router.navigate(['/login']);
      }
    });
  }
}
