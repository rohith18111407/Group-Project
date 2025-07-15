import { Component } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { textValidator } from '../validators/text-validator';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-signup',
  imports: [FormsModule,ReactiveFormsModule,CommonModule,RouterModule],
  templateUrl: './signup.html',
  styleUrl: './signup.css'
})
export class SignupComponent {
  signupForm = new FormGroup({
    username: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required, textValidator()]),
    roles: new FormControl(['Staff'])
  });

  errorMessage = '';

  constructor(private auth: AuthService, private router: Router) {}

  get username() { return this.signupForm.get('username'); }
  get password() { return this.signupForm.get('password'); }

  onSubmit() {
    if (this.signupForm.invalid) return;

    this.auth.register(this.signupForm.value as any).subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.errorMessage = 'Registration failed. Only Staff allowed.'
    });
  }
}
