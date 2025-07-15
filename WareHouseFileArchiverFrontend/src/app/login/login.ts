import { Component } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { textValidator } from '../validators/text-validator';
import { CommonModule } from '@angular/common';
import { AuthService } from '../services/auth.service';
import { Router, RouterModule } from '@angular/router';

@Component({
  selector: 'app-login',
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class LoginComponent {
  loginForm = new FormGroup({
    username: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required, textValidator()])
  });

  errorMessage = '';

  constructor(private auth: AuthService, private router: Router) { }

  get username() { return this.loginForm.get('username'); }
  get password() { return this.loginForm.get('password'); }

  onSubmit() {
    if (this.loginForm.invalid) return;

    this.auth.login(this.loginForm.value as any).subscribe({
      next: res => {
        const tokens = res.data;

        // Store all necessary values in sessionStorage
        this.auth.storeTokens(
          tokens.jwtToken,
          tokens.refreshToken,
          tokens.role,
          tokens.jwtExpiryTime
        );

        const role = this.auth.getUserRole();

        if (role === 'Admin') {
          this.router.navigate(['/admin/dashboard']);
        } else if (role === 'Staff') {
          this.router.navigate(['/staff/dashboard']);
        } else {
          this.errorMessage = 'Unauthorized role';
        }
      },
      error: () => {
        this.errorMessage = 'Invalid username or password.';
      }
    });
  }


}
